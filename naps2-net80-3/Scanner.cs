using NAPS2.Images;
using NAPS2.Images.Gdi;
using NAPS2.Pdf;
using NAPS2.Scan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace naps2_net80_3
{
    internal class Scanner
    {
        /// <summary>
        /// Scans all pages from the named device and exports them to a PDF file.
        /// Automatically selects the correct driver (32-bit TWAIN worker, native 64-bit TWAIN, or WIA).
        /// </summary>
        /// <param name="a_device">Display name of the scanner device to use.</param>
        /// <param name="a_outpdf">Full path of the PDF file to write.</param>
        /// <returns>Empty string if successful, otherwise an error message.</returns>
        public static async Task<string> ScanPdf(string a_device, string a_outpdf, ScannerOptions a_scannerOptions)
        {
            string msg = string.Empty;
            var (device, driver, needsWorker) = await FindDevice(a_device);

            if (device == null)
            {
                msg = $"Device not found: '{a_device}'";
                return msg;
            }

            using var scanningContext = new ScanningContext(new GdiImageContext());
            if (needsWorker)
            {
                Logger.Log("Using 32-bit TWAIN driver via Win32 worker.");
                scanningContext.SetUpWin32Worker();
            }

            var controller = new ScanController(scanningContext);

            BitDepth _bitDepth = BitDepth.Grayscale;
            if (driver == Driver.Wia)   // WIA often doesn't support grayscale, so use color for WIA devices
            {
                _bitDepth |= BitDepth.Grayscale;
            }
            else
            {
                switch (a_scannerOptions.BitDepth.ToLower())
                {
                    case "color":
                        _bitDepth = BitDepth.Color;
                        break;
                    case "bw":
                        _bitDepth = BitDepth.BlackAndWhite;
                        break;
                    default:
                        _bitDepth = BitDepth.Grayscale;
                        break;
                }
            }

            var options = new ScanOptions
            {
                Device = device,
                Driver = driver,
                PaperSource = PaperSource.Duplex,
                PageSize = GetPageSize(a_scannerOptions.PageSize),                      //PageSize.Letter,
                Dpi = int.TryParse(a_scannerOptions.Dpi, out int dpi) ? dpi : 300,
                BitDepth = _bitDepth,
                UseNativeUI = false
            };

            options.TwainOptions.Dsm = TwainDsm.NewX64;

            var images = new List<ProcessedImage>();
            try
            {
                await foreach (var image in controller.Scan(options))
                    images.Add(image);

                if (images.Count == 0)
                {
                    msg = "No pages were scanned.";
                    Logger.Log(msg);
                    return msg;
                }

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

                scanningContext.Dispose();
            }
            return msg;
        }

        /// <summary>
        /// Returns information about every scanner device visible through TWAIN (32-bit
        /// worker, then native 64-bit) and WIA.  Duplicates across drivers are included
        /// so the caller can see which drivers expose a given device.
        /// </summary>
        public static async Task<List<DeviceInfo>> GetDevicesAsync()
        {
            var results = new List<DeviceInfo>();

            // 1. 32-bit TWAIN via Win32 worker
            try
            {
                using var ctx = new ScanningContext(new GdiImageContext());
                ctx.SetUpWin32Worker();
                foreach (var d in await new ScanController(ctx).GetDeviceList(Driver.Twain))
                    results.Add(DeviceInfo.FromScanDevice(d, Driver.Twain, requiresWin32Worker: true));
            }
            catch { /* driver not available on this machine */ }

            var stop1 = 0;


            // 2. Native 64-bit TWAIN
            try
            {
                using var ctx = new ScanningContext(new GdiImageContext());
                foreach (var d in await new ScanController(ctx).GetDeviceList(Driver.Twain))
                    results.Add(DeviceInfo.FromScanDevice(d, Driver.Twain, requiresWin32Worker: false));
            }
            catch { /* driver not available on this machine */ }

            // 3. WIA fallback
            try
            {
                using var ctx = new ScanningContext(new GdiImageContext());
                foreach (var d in await new ScanController(ctx).GetDeviceList(Driver.Wia))
                    results.Add(DeviceInfo.FromScanDevice(d, Driver.Wia, requiresWin32Worker: false));
            }
            catch { /* driver not available on this machine */ }

            return results;
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

        private static PageSize GetPageSize(string pageSize)
        {
            return pageSize.ToLower() switch
            {
                "letter" => PageSize.Letter,
                "legal" => PageSize.Legal,
                "a4" => PageSize.A4,
                _ => PageSize.Letter
            };
        }


    }

    internal class ScannerOptions
    {
        public string BitDepth { get; set; } = "color";
        public string Dpi { get; set; } = "300";
        public string PageSize { get; set; } = "letter";
        public string Source { get; set; }  = "duplex";
    }
}
