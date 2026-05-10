using NAPS2.Images;
using NAPS2.Images.Gdi;
using NAPS2.Pdf;
using NAPS2.Scan;

namespace naps2_net80_1
{
    internal class Scanner
    {
        /// <summary>
        /// Scans all pages from the named device and exports them to a PDF file.
        /// Automatically selects the correct driver (32-bit TWAIN worker, native 64-bit TWAIN, or WIA).
        /// </summary>
        /// <param name="a_device">Display name of the scanner device to use.</param>
        /// <param name="a_outpdf">Full path of the PDF file to write.</param>
        public static async Task ScanPdf(string a_device, string a_outpdf)
        {
            var (device, driver, needsWorker) = await FindDevice(a_device);

            if (device == null)
                throw new InvalidOperationException($"Device not found: '{a_device}'");

            using var scanningContext = new ScanningContext(new GdiImageContext());
            if (needsWorker)
                scanningContext.SetUpWin32Worker();

            var controller = new ScanController(scanningContext);

            var options = new ScanOptions
            {
                Device = device,
                Driver = driver,
                PaperSource = PaperSource.Duplex,
                PageSize = PageSize.Letter,
                Dpi = 300,
                BitDepth = BitDepth.Grayscale
            };

            var images = new List<ProcessedImage>();
            try
            {
                await foreach (var image in controller.Scan(options))
                    images.Add(image);

                if (images.Count == 0)
                    throw new InvalidOperationException("No pages were scanned.");

                var outDir = Path.GetDirectoryName(a_outpdf);
                if (!string.IsNullOrEmpty(outDir))
                    Directory.CreateDirectory(outDir);

                var pdfExporter = new PdfExporter(scanningContext);
                await pdfExporter.Export(a_outpdf, images);
            }
            finally
            {
                foreach (var image in images)
                    image.Dispose();
            }
        }

        /// <summary>
        /// Finds the named device in priority order:
        /// 1. 32-bit TWAIN via Win32 worker
        /// 2. Native 64-bit TWAIN
        /// 3. WIA fallback
        /// </summary>
        private static async Task<(ScanDevice? device, Driver driver, bool needsWorker)> FindDevice(string deviceName)
        {
            // 1. 32-bit TWAIN via Win32 worker
            using (var ctx = new ScanningContext(new GdiImageContext()))
            {
                ctx.SetUpWin32Worker();
                var d = (await new ScanController(ctx).GetDeviceList(Driver.Twain))
                    .FirstOrDefault(x => x.Name == deviceName);
                if (d != null) return (d, Driver.Twain, true);
            }

            // 2. Native 64-bit TWAIN
            using (var ctx = new ScanningContext(new GdiImageContext()))
            {
                var d = (await new ScanController(ctx).GetDeviceList(Driver.Twain))
                    .FirstOrDefault(x => x.Name == deviceName);
                if (d != null) return (d, Driver.Twain, false);
            }

            // 3. WIA fallback
            using (var ctx = new ScanningContext(new GdiImageContext()))
            {
                var d = (await new ScanController(ctx).GetDeviceList(Driver.Wia))
                    .FirstOrDefault(x => x.Name == deviceName);
                if (d != null) return (d, Driver.Wia, false);
            }

            return (null, Driver.Default, false);
        }
    }
}
