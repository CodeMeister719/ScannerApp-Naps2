using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace naps2_net80_3
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string msg = string.Empty;
            var scannerOptions = MainArgsScannerOptions(args);
            string output = GetArgValue(args, "--output", "");

            // If output is empty, generate default path; if no extension, default to .pdf
            string outPdf = string.IsNullOrEmpty(output)
                ? Path.Combine("c:\\temp\\ScannedImages", $"twain_{DateTime.Now:yyyyMMddHHmmssfff}.pdf")
                : string.IsNullOrEmpty(Path.GetExtension(output)) ? output + ".pdf" : output;

            // Initialize logging with the same path as output PDF but with .log extension
            Logger.Initialize(outPdf);
            Logger.Log($"--output {outPdf}");
            Logger.Log($"--bitdepth {scannerOptions.BitDepth}");
            Logger.Log($"--dpi {scannerOptions.Dpi}");
            Logger.Log($"--pagesize {scannerOptions.PageSize}");
            Logger.Log($"--source {scannerOptions.Source}");

            // Get list of scanner devices found connected.
            var devices = await Scanner.GetDevicesAsync();

            if (devices.Count == 0)
            {
                msg = $"No scanner devices found.";
                Logger.Log(msg);
            }
            else
            {
                Logger.Log($"Found {devices.Count} device(s):");
                foreach (var device in devices)
                     Logger.Log($"{device}");

                // Get the first device that matches any of the targets specified in the fj-devicelist.txt file (case-insensitive substring match).
                var targets = DeviceListFromFile();
                var selectedDevice = devices.FirstOrDefault(d =>
                    targets.Any(t => d.Name.Contains(t, StringComparison.OrdinalIgnoreCase)));

                if (selectedDevice == null)
                {
                    msg = $"No device found matching targets: {string.Join(", ", targets)}";
                    Logger.Log(msg);
                }
                else
                {
                    Logger.Log($"Selected device: {selectedDevice.Name}");
                    msg = await Scanner.ScanPdf(selectedDevice.Name, outPdf, scannerOptions);
                    if (!string.IsNullOrEmpty(msg))
                    {
                        msg = $"Scan failed: {msg}";
                        Logger.Log(msg);
                    }
                    else
                    {
                        msg = "";
                        Logger.Log($"Program finished. Output PDF: {outPdf}");
                    }
                }

                bool success = string.IsNullOrEmpty(msg);
                string json = JsonSerializer.Serialize(new
                {
                    success = success,
                    message = msg
                });
                string jsonPath = Path.ChangeExtension(outPdf, ".json");
                File.WriteAllText(jsonPath, json);

                return;
            }
        }


        static string GetArgValue(string[] args, string key, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return defaultValue;
        }
        static int GetArgValue(string[] args, string key, int defaultValue)
        {
            string value = GetArgValue(args, key, "");
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        static bool GetArgValue(string[] args, string key, bool defaultValue)
        {
            string value = GetArgValue(args, key, "");
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        static ScannerOptions MainArgsScannerOptions(string[] args)
        {
            string output = GetArgValue(args, "--output", "");

            return new ScannerOptions
            {
                BitDepth = GetArgValue(args, "--bitdepth", "color"),
                Dpi = GetArgValue(args, "--dpi", "300"),
                PageSize = GetArgValue(args, "--pagesize", "letter"),
                Source = GetArgValue(args, "--source", "duplex")
            };
        }

        static string[] DeviceListFromFile()
        {
            const string filePath = "fj-devicelist.txt";

            if (!File.Exists(filePath))
            {
                Logger.Log($"Device list file {filePath} not found. No devices will be loaded from file.");
                return Array.Empty<string>();
            }

            string[] deviceNames = File.ReadAllLines(filePath)
                .SelectMany(line => line.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();

            return deviceNames;
        }

    }
}
