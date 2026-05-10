using NAPS2.Images;
using NAPS2.Images.Gdi;
using NAPS2.Pdf;
using NAPS2.Scan;
using System.Collections.Generic;


namespace naps2_net80_1
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //Test1().GetAwaiter().GetResult();
            

            await Scanner.ScanPdf("PaperStream IP fi-7160", @"C:\temp\ScannedImages\scan.pdf");

        }

        static async Task Test1()
        {
            // Probe for a scanner without assuming driver bitness
            var (device, driver, needsWorker) = await FindDevice();

            if (device == null)
                throw new InvalidOperationException("No scanner found via TWAIN (32-bit or 64-bit) or WIA.");

            Console.WriteLine($"Using device: {device.Name} | Driver: {driver} | Win32Worker: {needsWorker}");

            using var scanningContext = new ScanningContext(new GdiImageContext());
            if (needsWorker)
                scanningContext.SetUpWin32Worker();
            var controller = new ScanController(scanningContext);

            var options = new ScanOptions
            {
                Device = device,
                Driver = driver,
                PaperSource = PaperSource.Duplex,
                PageSize = PageSize.A4,
                Dpi = 300,
                BitDepth = BitDepth.Grayscale   // or .Color or .BlackAndWhite
            };

            // Scan once, collect all pages
            var images = new List<ProcessedImage>();
            await foreach (var image in controller.Scan(options))
                images.Add(image);

            // Save each page as a JPEG
            for (int i = 0; i < images.Count; i++)
                images[i].Save($"page{i + 1}.jpg");

            // Export all pages to PDF
            var pdfExporter = new PdfExporter(scanningContext);
            await pdfExporter.Export("doc.pdf", images);
        }

        /// <summary>
        /// Probes for an available scanner in priority order:
        /// 1. TWAIN via 32-bit Win32 worker (most scanner TWAIN drivers are 32-bit)
        /// 2. Native 64-bit TWAIN (no worker needed)
        /// 3. WIA fallback
        /// </summary>
        static async Task<(ScanDevice? device, Driver driver, bool needsWorker)> FindDevice()
        {
            // 1. 32-bit TWAIN via Win32 worker
            using (var ctx = new ScanningContext(new GdiImageContext()))
            {
                ctx.SetUpWin32Worker();
                var d = (await new ScanController(ctx).GetDeviceList(Driver.Twain)).FirstOrDefault();
                if (d != null) return (d, Driver.Twain, true);
            }

            // 2. Native 64-bit TWAIN (no worker)
            using (var ctx = new ScanningContext(new GdiImageContext()))
            {
                var d = (await new ScanController(ctx).GetDeviceList(Driver.Twain)).FirstOrDefault();
                if (d != null) return (d, Driver.Twain, false);
            }

            // 3. WIA fallback
            using (var ctx = new ScanningContext(new GdiImageContext()))
            {
                var d = (await new ScanController(ctx).GetDeviceList(Driver.Wia)).FirstOrDefault();
                if (d != null) return (d, Driver.Wia, false);
            }

            return (null, Driver.Default, false);
        }

    }
}
