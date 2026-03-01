using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace D2RFastLoad.Services;

public static class AudioService
{
    private static string  _audioDir  = "";
    private static double  _volume    = 0.7;
    private static bool    _muted     = false;
    private static bool    _minimal   = false;

    // MediaPlayer must live on the UI thread; we create it lazily there.
    private static MediaPlayer? _player;

    private static readonly string[] MinimalOnly = ["launch", "success", "error"];

    // ── Init ──────────────────────────────────────────────────────────────────

    public static void Init()
    {
        _audioDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "D2RFastLoad", "audio");
        Directory.CreateDirectory(_audioDir);
        DeployEmbedded();

        // Create MediaPlayer on the UI thread
        Application.Current.Dispatcher.Invoke(() => _player = new MediaPlayer());
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    public static void SetVolume(double v)  => _volume  = Math.Clamp(v / 100.0, 0, 1);
    public static void SetMuted(bool m)     => _muted   = m;
    public static void SetMinimal(bool m)   => _minimal = m;

    // ── Play ──────────────────────────────────────────────────────────────────

    public static void Play(string sfxName)
    {
        if (_muted) return;
        if (_minimal && !MinimalOnly.Contains(sfxName)) return;

        var path = Path.Combine(_audioDir, sfxName + ".mp3");
        if (!File.Exists(path)) return;

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_player == null) return;
            _player.Stop();
            _player.Volume = _volume;
            _player.Open(new Uri(path, UriKind.Absolute));
            _player.Play();
        });
    }

    // ── Deploy embedded MP3s ─────────────────────────────────────────────────

    private static void DeployEmbedded()
    {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
        {
            // Embedded names look like: D2RFastLoad.Resources.audio.press.mp3
            if (!name.Contains(".audio.") || !name.EndsWith(".mp3")) continue;

            var parts    = name.Split('.');
            var fileBase = parts.Length >= 2 ? parts[^2] : "sfx";
            var dest     = Path.Combine(_audioDir, fileBase + ".mp3");
            if (File.Exists(dest)) continue;

            try
            {
                using var src = asm.GetManifestResourceStream(name)!;
                using var dst = File.Create(dest);
                src.CopyTo(dst);
            }
            catch { /* missing audio is non-fatal */ }
        }
    }
}
