using System.IO;

namespace D2RFastLoad.Services;

public static class LogService
{
    private static string _logPath = "";

    /// <summary>Called on every log line — set to a UI dispatcher callback.</summary>
    public static Action<string>? OnLog;

    public static string LogDir  { get; private set; } = "";
    public static string LogPath => _logPath;

    public static void Init()
    {
        LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D2RFastLoad", "logs");
        Directory.CreateDirectory(LogDir);
        _logPath = Path.Combine(LogDir, $"d2rfastload_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    }

    public static void Write(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        try { File.AppendAllText(_logPath, line + Environment.NewLine); } catch { }
        OnLog?.Invoke(line);
    }
}
