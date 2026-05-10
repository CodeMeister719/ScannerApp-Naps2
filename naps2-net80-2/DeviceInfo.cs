using NAPS2.Scan;

namespace naps2_net80_1
{
    /// <summary>
    /// Holds information about a discovered scanner device.
    /// </summary>
    internal class DeviceInfo
    {
        /// <summary>Device display name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Device identifier string reported by the driver.</summary>
        public string DeviceId { get; init; } = string.Empty;

        /// <summary>The driver used to discover this device.</summary>
        public Driver Driver { get; init; }

        /// <summary>Whether the device requires a 32-bit Win32 worker process.</summary>
        public bool RequiresWin32Worker { get; init; }

        /// <summary>
        /// Manufacturer / vendor name extracted from the device ID when available.
        /// The TWAIN and WIA device IDs often embed the vendor name as the first
        /// backslash-delimited segment (e.g. "WIA\\FUJITSU fi-7160").
        /// </summary>
        public string Manufacturer { get; init; } = string.Empty;

        /// <summary>
        /// Version / model token extracted from the device ID when available
        /// (the segment after the vendor, if present).
        /// </summary>
        public string VersionInfo { get; init; } = string.Empty;

        public override string ToString() =>
            $"[{Driver}{(RequiresWin32Worker ? "/Win32" : "")}] " +
            $"{Name}" +
            (string.IsNullOrEmpty(Manufacturer) ? "" : $" | Vendor: {Manufacturer}") +
            (string.IsNullOrEmpty(VersionInfo)  ? "" : $" | Version: {VersionInfo}") +
            $" | ID: {DeviceId}";

        /// <summary>
        /// Builds a <see cref="DeviceInfo"/> from a raw <see cref="ScanDevice"/>.
        /// </summary>
        internal static DeviceInfo FromScanDevice(ScanDevice scanDevice, Driver driver, bool requiresWin32Worker)
        {
            // Try to parse vendor / version from the device ID.
            // Common formats:
            //   WIA:  "WIA\VEN_FUJITSU&DEV_fi-7160&..."  or  "WIA\\FUJITSU fi-7160"
            //   TWAIN: "FUJITSU fi-7160.1" or plain name
            string manufacturer = string.Empty;
            string versionInfo   = string.Empty;

            var id = scanDevice.ID ?? string.Empty;

            // Format 1 – backslash-separated segments
            var parts = id.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                manufacturer = parts[0].Trim();
                versionInfo  = parts[1].Trim();
            }
            else
            {
                // Format 2 – "VEN_xxx&DEV_yyy" style
                var ven = ExtractTag(id, "VEN_");
                var dev = ExtractTag(id, "DEV_");
                if (!string.IsNullOrEmpty(ven)) manufacturer = ven;
                if (!string.IsNullOrEmpty(dev)) versionInfo  = dev;
            }

            return new DeviceInfo
            {
                Name                = scanDevice.Name ?? string.Empty,
                DeviceId            = id,
                Driver              = driver,
                RequiresWin32Worker = requiresWin32Worker,
                Manufacturer        = manufacturer,
                VersionInfo         = versionInfo,
            };
        }

        private static string ExtractTag(string source, string tag)
        {
            int start = source.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return string.Empty;
            start += tag.Length;
            int end = source.IndexOfAny(['&', ';', ' '], start);
            return end < 0 ? source[start..] : source[start..end];
        }
    }
}
