using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace D2RFastLoad.Services;

public static class ExtractionService
{
    private static string _extractorPath = "";

    // ── Deploy extractor ──────────────────────────────────────────────────────

    /// <summary>
    /// Writes the embedded d2r_bulk_extract.exe to LocalAppData and returns its path.
    /// Returns empty string if the resource was not embedded.
    /// </summary>
    public static string DeployExtractor()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D2RFastLoad");
        Directory.CreateDirectory(dir);
        _extractorPath = Path.Combine(dir, "d2r_bulk_extract.exe");

        var asm     = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
                         .FirstOrDefault(n => n.EndsWith("d2r_bulk_extract.exe"));

        if (resName == null) return "";   // not embedded — show friendly error upstream

        try
        {
            using var src = asm.GetManifestResourceStream(resName)!;
            using var dst = File.Create(_extractorPath);
            src.CopyTo(dst);
        }
        catch { return ""; }

        return _extractorPath;
    }

    // ── Run ───────────────────────────────────────────────────────────────────

    public static async Task<bool> RunExtractorAsync(
        string        installPath,
        Action<string> onOutput,
        Action<int>   onProgress,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_extractorPath) || !File.Exists(_extractorPath))
        {
            onOutput("ERROR: Extractor not deployed.");
            return false;
        }

        var args = $"\"{installPath}\"";

        var psi = new ProcessStartInfo(_extractorPath, args)
        {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            WorkingDirectory       = installPath
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            onOutput(e.Data);
            // Parse "Progress: 42%" lines if the tool emits them
            if (e.Data.StartsWith("Progress:", StringComparison.OrdinalIgnoreCase)
                && e.Data.Contains('%'))
            {
                var token = e.Data.Split(':')[1].Trim().TrimEnd('%');
                if (int.TryParse(token, out var pct)) onProgress(pct);
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) onOutput("ERR: " + e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(true); } catch { }
            throw;
        }

        return proc.ExitCode == 0;
    }

    // ── Post-processing ───────────────────────────────────────────────────────

    public static void SaveVersionStamp(string installPath, string version)
    {
        var stampPath = Path.Combine(installPath, "Data", "_adramadus_version.txt");
        try { File.WriteAllText(stampPath, version); } catch { }
    }

    /// <summary>Delete the entire Data\hd\ folder (SD-only mode — saves ~37 GB).</summary>
    public static void DeleteHdFolder(string installPath)
    {
        var hdPath = Path.Combine(installPath, "Data", "hd");
        if (Directory.Exists(hdPath))
            Directory.Delete(hdPath, true);
    }

    /// <summary>Delete *.model and *.texture files from Data\hd\ (load speed boost).</summary>
    public static void DeleteHdModelTextureFiles(string installPath)
    {
        var hdPath = Path.Combine(installPath, "Data", "hd");
        if (!Directory.Exists(hdPath)) return;
        foreach (var ext in new[] { "*.model", "*.texture" })
            foreach (var f in Directory.GetFiles(hdPath, ext, SearchOption.AllDirectories))
                try { File.Delete(f); } catch { }
    }

    /// <summary>Delete the lobby UI folder from Data\hd\ (saves ~320 MB, offline-only).</summary>
    public static void DeleteLobbyFolder(string installPath)
    {
        var lobbyPath = Path.Combine(installPath, "Data", "hd", "global", "ui", "lobby");
        if (Directory.Exists(lobbyPath))
            Directory.Delete(lobbyPath, true);
    }
}
