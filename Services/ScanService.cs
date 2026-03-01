using System.Diagnostics;
using System.IO;
using D2RFastLoad.Models;

namespace D2RFastLoad.Services;

public static class ScanService
{
    private static readonly string[] KnownPaths =
    [
        @"C:\Program Files (x86)\Diablo II Resurrected",
        @"C:\Program Files\Diablo II Resurrected",
        @"D:\Diablo II Resurrected",
        @"E:\Diablo II Resurrected",
    ];

    public static string FindD2RPath()
    {
        // 1. Registry (Battle.net install record)
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\WOW6432Node\Blizzard Entertainment\Diablo II Resurrected");
            if (key?.GetValue("InstallPath") is string reg && Directory.Exists(reg))
                return reg;
        }
        catch { }

        // 2. Well-known paths
        foreach (var p in KnownPaths)
            if (File.Exists(Path.Combine(p, "D2R.exe"))) return p;

        return "";
    }

    public static ScanResult Scan(string installPath)
    {
        var result = new ScanResult { InstallPath = installPath };

        if (!File.Exists(Path.Combine(installPath, "D2R.exe")))
            return result;

        // Game version from exe
        var vi = FileVersionInfo.GetVersionInfo(Path.Combine(installPath, "D2R.exe"));
        result.GameVersion = vi.FileVersion ?? "";

        // Extraction status
        result.IsExtracted = Directory.Exists(Path.Combine(installPath, "Data", "global")) &&
                             Directory.Exists(Path.Combine(installPath, "Data", "hd"))    &&
                             Directory.Exists(Path.Combine(installPath, "Data", "local"));

        // Version stamp match
        var stampPath = Path.Combine(installPath, "Data", "_adramadus_version.txt");
        if (File.Exists(stampPath))
        {
            result.ExtractedVersion = File.ReadAllText(stampPath).Trim();
            result.VersionOk = result.ExtractedVersion == result.GameVersion;
        }

        // Launch args
        var args = ConfigService.GetLaunchArgs();
        result.DirectActive = args.Contains("-direct", StringComparison.OrdinalIgnoreCase);

        // VSync / FPS Cap
        result.VsyncOff   = ConfigService.GetSetting("VSync") == 0;
        result.FpsCapOff  = ConfigService.GetSetting("Framerate Cap") == 0;

        // Firewall
        result.FirewallBlock = FirewallService.RuleExists();

        return result;
    }

    public static bool IsD2RRunning() =>
        Process.GetProcessesByName("D2R").Length > 0;

    public static void StopBattleNet()
    {
        foreach (var name in new[] { "Battle.net", "Agent", "BlizzardError" })
            foreach (var p in Process.GetProcessesByName(name))
                try { p.Kill(); } catch { }
    }
}
