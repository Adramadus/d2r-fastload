using System.Management;

namespace D2RFastLoad.Services;

public static class HwProfileService
{
    /// <summary>
    /// Returns a one-line hardware summary string, e.g.
    /// "CPU: Ryzen 9 3900X  ·  GPU: RX 6700 XT  ·  RAM: 32 GB"
    /// Returns empty string on any failure (non-fatal).
    /// </summary>
    public static string GetProfile()
    {
        try
        {
            var cpu = WmiFirst("Win32_Processor",       "Name");
            var gpu = WmiFirst("Win32_VideoController", "Name");
            var ram = WmiFirst("Win32_ComputerSystem",  "TotalPhysicalMemory");

            long ramGb = string.IsNullOrEmpty(ram) ? 0
                       : long.Parse(ram) / (1024L * 1024 * 1024);

            return $"CPU: {cpu}  ·  GPU: {gpu}  ·  RAM: {ramGb} GB";
        }
        catch { return ""; }
    }

    private static string WmiFirst(string wmiClass, string property)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
        foreach (ManagementObject obj in searcher.Get())
            return obj[property]?.ToString()?.Trim() ?? "";
        return "";
    }
}
