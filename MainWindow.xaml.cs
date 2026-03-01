using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using D2RFastLoad.Models;
using D2RFastLoad.Services;

namespace D2RFastLoad;

public partial class MainWindow : Window
{
    private ScanResult? _scan;
    private bool        _opRunning;
    private bool        _muted;

    private readonly DispatcherTimer _opTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _opTimerSecs;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        _opTimer.Tick += (_, _) =>
        {
            _opTimerSecs++;
            sbTimer.Text = $"{_opTimerSecs / 60}:{_opTimerSecs % 60:00}";
        };
        Loaded += OnLoaded;
    }

    // ── Window loaded ─────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LogService.Init();
        LogService.OnLog = line => Dispatcher.BeginInvoke(() => AppendLog(line));

        AudioService.Init();

        // Restore last known path
        var state = StateService.Load();
        txtPath.Text = state.LastPath;
        if (string.IsNullOrEmpty(txtPath.Text))
            txtPath.Text = ScanService.FindD2RPath();

        // Version label
        var ver = System.Reflection.Assembly.GetExecutingAssembly()
                      .GetName().Version?.ToString(3) ?? "2.0.0";
        lblAppVer.Text = $"v{ver}";

        // Players combo
        cmbPlayers.Items.Add("Off");
        for (int i = 1; i <= 8; i++) cmbPlayers.Items.Add(i.ToString());
        cmbPlayers.SelectedIndex = 0;

        sbLogLink.Text = "Open Logs";

        WireEvents();

        if (!string.IsNullOrEmpty(txtPath.Text))
            DoScan();

        // Show startup info after first render
        Dispatcher.BeginInvoke(ShowStartupReminder, DispatcherPriority.Loaded);

        LogService.Write($"D2R Fast Load v{ver} started.");
    }

    // ── Event wiring ──────────────────────────────────────────────────────────

    private void WireEvents()
    {
        btnScan.Click    += (_, _) => DoScan();
        btnBrowse.Click  += BrowseClick;
        btnExtract.Click += async (_, _) => await ExtractClickAsync();
        btnApply.Click   += (_, _) => ApplyClick();
        btnRevert.Click  += (_, _) => RevertClick();
        btnVerify.Click  += (_, _) => VerifyClick();
        btnLogs.Click    += (_, _) => OpenLogFolder();
        btnRamDisk.Click += (_, _) => RamDiskClick();

        btnSettings.Click  += (_, _) => settingsPopup.IsOpen = !settingsPopup.IsOpen;
        btnMute.Click      += (_, _) => ToggleMute();
        btnTestAudio.Click += (_, _) => AudioService.Play("success");

        btnSelectAll.Click += (_, _) => SelectAll(true);
        btnClearAll.Click  += (_, _) => SelectAll(false);

        sbLogLink.MouseLeftButtonUp += (_, _) => OpenLogFolder();

        sldVolume.ValueChanged += (_, e) =>
        {
            AudioService.SetVolume(e.NewValue);
            lblVolPct.Text = $"{(int)e.NewValue}%";
        };

        chkMuteOpt.Checked   += (_, _) => { _muted = true;  AudioService.SetMuted(true);  btnMute.Content = "\U0001F507"; };
        chkMuteOpt.Unchecked += (_, _) => { _muted = false; AudioService.SetMuted(false); btnMute.Content = "\u266A"; };
        chkMinSFX.Checked    += (_, _) => AudioService.SetMinimal(true);
        chkMinSFX.Unchecked  += (_, _) => AudioService.SetMinimal(false);

        // Re-scan when path changes
        txtPath.LostFocus += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(txtPath.Text)) DoScan();
        };
    }

    // ── Startup reminder ──────────────────────────────────────────────────────

    private void ShowStartupReminder()
    {
        MessageBox.Show(this,
            "D2R Fast Load extracts and optimizes Diablo II: Resurrected game files for direct" + Environment.NewLine +
            "file access, cutting load times between acts from 10+ seconds to under 2 seconds." + Environment.NewLine + Environment.NewLine +
            "Click  SCAN  before applying any settings." + Environment.NewLine + Environment.NewLine +
            "Scan detects your D2R installation and checks whether extraction is complete" + Environment.NewLine +
            "so the correct options are unlocked.",
            "D2R Fast Load  -  Start Here",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ── Scan ──────────────────────────────────────────────────────────────────

    private void DoScan()
    {
        var path = txtPath.Text.Trim();
        if (string.IsNullOrEmpty(path)) { SetStatus("No path — click Browse", null); return; }

        AudioService.Play("scan");
        SetStatus("Scanning...", null);
        LogService.Write($"Scanning: {path}");

        _scan = ScanService.Scan(path);
        UpdateStatusGrid(_scan);
        ApplyCapabilityGating(_scan);
        UpdateStepIndicator(_scan);

        // Persist last path
        var state = StateService.Load();
        state.LastPath = path;
        StateService.Save(state);

        // HW profile — background, non-blocking
        Task.Run(() =>
        {
            var profile = HwProfileService.GetProfile();
            if (!string.IsNullOrEmpty(profile))
                Dispatcher.BeginInvoke(() =>
                {
                    lblHW.Text             = profile;
                    pnlHW.Visibility = Visibility.Visible;
                });
        });

        LogService.Write($"Scan done — Extracted={_scan.IsExtracted}, VersionOk={_scan.VersionOk}, " +
                         $"Direct={_scan.DirectActive}, Game={_scan.GameVersion}");
        SetStatus("Scan complete", null);
        AudioService.Play("success");
    }

    // ── Status grid update ────────────────────────────────────────────────────

    private void UpdateStatusGrid(ScanResult s)
    {
        lblD2RInfo.Text = string.IsNullOrEmpty(s.GameVersion)
            ? "D2R not found at this path"
            : $"D2R {s.GameVersion}  \u00B7  {s.InstallPath}";

        ellInstall.Fill = string.IsNullOrEmpty(s.GameVersion)
            ? new SolidColorBrush(Color.FromRgb(0x40, 0x10, 0x10))
            : new SolidColorBrush(Color.FromRgb(0x10, 0x50, 0x20));

        // Extraction
        if (s.IsExtracted)
        {
            lblExtStatus.Text       = s.VersionOk ? "Extracted \u2713" : "Extracted \u26A0";
            lblExtStatus.Foreground = s.VersionOk
                ? new SolidColorBrush(Color.FromRgb(0x40, 0xD0, 0x70))
                : new SolidColorBrush(Color.FromRgb(0xE0, 0x90, 0x30));
            lblExtDetail.Text = s.VersionOk
                ? $"Version match: {s.ExtractedVersion}"
                : $"MISMATCH  Game: {s.GameVersion}  Stamp: {s.ExtractedVersion}  Re-extract!";
        }
        else
        {
            lblExtStatus.Text       = "Not extracted";
            lblExtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0x50, 0x50));
            lblExtDetail.Text       = "Run Extract + Optimize to enable fast loads";
        }

        // -direct
        lblDirectStatus.Text       = s.DirectActive ? "Active \u2713" : "Inactive";
        lblDirectStatus.Foreground = new SolidColorBrush(s.DirectActive
            ? Color.FromRgb(0x40, 0xD0, 0x70)
            : Color.FromRgb(0xD0, 0x50, 0x50));
        lblDirectDetail.Text = s.DirectActive ? "-direct -txt in launch args" : "Not set";

        // VSync
        lblVsyncStatus.Text       = s.VsyncOff ? "Off \u2713" : "On";
        lblVsyncStatus.Foreground = new SolidColorBrush(s.VsyncOff
            ? Color.FromRgb(0x40, 0xD0, 0x70)
            : Color.FromRgb(0xC0, 0xA0, 0x40));
        lblVsyncDetail.Text = s.VsyncOff ? "VSync=0" : "VSync=1 (default)";

        // FPS Cap
        lblFPSStatus.Text       = s.FpsCapOff ? "Uncapped \u2713" : "Capped!";
        lblFPSStatus.Foreground = new SolidColorBrush(s.FpsCapOff
            ? Color.FromRgb(0x40, 0xD0, 0x70)
            : Color.FromRgb(0xD0, 0x50, 0x50));
        lblFPSDetail.Text = s.FpsCapOff ? "Framerate Cap=0" : "Cap active \u2014 hurts loads!";

        // Firewall
        lblFWStatus.Text       = s.FirewallBlock ? "Blocked \u2713" : "Open";
        lblFWStatus.Foreground = new SolidColorBrush(s.FirewallBlock
            ? Color.FromRgb(0x40, 0xD0, 0x70)
            : Color.FromRgb(0x80, 0x80, 0xA0));
        lblFWDetail.Text = s.FirewallBlock ? "Outbound block active" : "No block rule";

        // Launch args
        var args = ConfigService.GetLaunchArgs();
        lblArgsStatus.Text = string.IsNullOrEmpty(args) ? "(none)" : args;

        // Version mismatch warning
        if (s.IsExtracted && !s.VersionOk)
        {
            lblWarning.Text =
                $"Game updated! Extraction stamp ({s.ExtractedVersion}) does not match " +
                $"game version ({s.GameVersion}). Re-extract to restore fast loads.";
            pnlWarning.Visibility = Visibility.Visible;
        }
        else
        {
            pnlWarning.Visibility = Visibility.Collapsed;
        }
    }

    // ── Capability gating ─────────────────────────────────────────────────────

    private void ApplyCapabilityGating(ScanResult s)
    {
        bool hasD2R       = !string.IsNullOrEmpty(s.GameVersion);
        bool needsExtract = !s.IsExtracted || !s.VersionOk;

        btnExtract.IsEnabled = hasD2R && needsExtract;
        btnApply.IsEnabled   = hasD2R;
        btnRevert.IsEnabled  = hasD2R;
        btnVerify.IsEnabled  = hasD2R;

        SetStatus(
            !hasD2R       ? "D2R not found \u2014 check path or click Browse" :
            needsExtract  ? "Ready \u2014 Extract required for fast loads" :
                            "Ready", null);
    }

    // ── Step indicator ────────────────────────────────────────────────────────

    private void UpdateStepIndicator(ScanResult s)
    {
        for (int i = 0; i <= 5; i++) SetStep(i, "idle");
        if (!string.IsNullOrEmpty(s.GameVersion))          SetStep(0, "done");
        if (s.IsExtracted)                                  SetStep(2, "done");
        if (s.DirectActive)                                 SetStep(3, "done");
        if (s.IsExtracted && s.VersionOk && s.DirectActive) { SetStep(4, "done"); SetStep(5, "done"); }
    }

    private void SetStep(int i, string state)
    {
        if (FindName($"stepCircle{i}") is not System.Windows.Controls.Border     circle) return;
        if (FindName($"stepText{i}")   is not System.Windows.Controls.TextBlock   text)  return;

        (circle.Background, circle.BorderBrush, text.Foreground) = state switch
        {
            "active" => (
                (Brush)new SolidColorBrush(Color.FromRgb(0xA0, 0x50, 0x10)),
                (Brush)new SolidColorBrush(Color.FromRgb(0xE0, 0x88, 0x28)),
                (Brush)Brushes.White),
            "done" => (
                new SolidColorBrush(Color.FromRgb(0x0A, 0x30, 0x20)),
                new SolidColorBrush(Color.FromRgb(0x20, 0xA0, 0x60)),
                new SolidColorBrush(Color.FromRgb(0x60, 0xD0, 0x90))),
            _ => (
                new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x28)),
                new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x40)),
                new SolidColorBrush(Color.FromRgb(0x78, 0x78, 0xA0)))
        };
    }

    // ── Browse ────────────────────────────────────────────────────────────────

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title           = "Select D2R.exe",
            Filter          = "D2R.exe|D2R.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true)
        {
            txtPath.Text = Path.GetDirectoryName(dialog.FileName) ?? "";
            DoScan();
        }
    }

    // ── Extract + Optimize ────────────────────────────────────────────────────

    private async Task ExtractClickAsync()
    {
        if (_opRunning) return;

        var path = txtPath.Text.Trim();
        if (string.IsNullOrEmpty(path)) return;

        if (ScanService.IsD2RRunning())
        {
            MessageBox.Show(this, "D2R is currently running. Close it before extracting.",
                "D2R Running", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(this,
            "This will extract CASC archives to loose files (roughly 42 GB, up to 65 minutes).\n\n" +
            "Battle.net will be closed automatically.\n\n" +
            "Continue?",
            "Extract + Optimize", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        // Capture UI state on UI thread before async work begins
        bool sdOnly      = chkSDOnly.IsChecked        == true;
        bool delHdFiles  = chkDeleteHDFiles.IsChecked == true;
        bool delLobby    = chkDeleteLobby.IsChecked   == true;
        bool audioDegrade = chkAudioDegrade.IsChecked == true;
        bool reduceGfx   = chkReduceGfx.IsChecked     == true;
        bool firewall    = chkFirewall.IsChecked       == true;
        var  launchArgs  = BuildLaunchArgs();
        var  gameVer     = _scan?.GameVersion ?? "";

        SetUiEnabled(false);
        StartOpTimer();
        AudioService.Play("launch");
        SetStep(2, "active");
        progBar.IsIndeterminate = true;

        bool success = false;
        try
        {
            success = await Task.Run(() => RunExtractionCore(
                path, sdOnly, delHdFiles, delLobby, audioDegrade,
                reduceGfx, firewall, launchArgs, gameVer));
        }
        finally
        {
            progBar.IsIndeterminate = false;
            StopOpTimer();
            SetUiEnabled(true);
            DoScan();
        }

        if (success)
            MessageBox.Show(this,
                "Extraction and optimization complete!\n\n" +
                "D2R will now load loose files directly.\nFast loads are active.",
                "Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        else
            AudioService.Play("error");
    }

    private bool RunExtractionCore(
        string path, bool sdOnly, bool delHdFiles,
        bool delLobby, bool audioDegrade, bool reduceGfx, bool firewall,
        string launchArgs, string gameVer)
    {
        LogService.Write("=== Extract + Optimize started ===");

        LogService.Write("Stopping Battle.net...");
        ScanService.StopBattleNet();

        LogService.Write("Deploying extractor...");
        var extractorPath = ExtractionService.DeployExtractor();
        if (string.IsNullOrEmpty(extractorPath))
        {
            LogService.Write("ERROR: d2r_bulk_extract.exe not found in resources. Place it in Resources\\ and rebuild.");
            Dispatcher.Invoke(() =>
                MessageBox.Show(this,
                    "The extraction tool (d2r_bulk_extract.exe) is not embedded in this build.\n\n" +
                    "Place d2r_bulk_extract.exe in the Resources\\ folder and rebuild the project.",
                    "Extractor Missing", MessageBoxButton.OK, MessageBoxImage.Error));
            return false;
        }

        Dispatcher.Invoke(() => { progBar.IsIndeterminate = false; SetStatus("Extracting CASC archives...", 0); });

        // Run extraction subprocess (blocking — we're on a background thread)
        var task = ExtractionService.RunExtractorAsync(
            path,
            msg => LogService.Write(msg),
            pct => Dispatcher.BeginInvoke(() => SetProgress(pct)),
            CancellationToken.None);

        bool ok = task.GetAwaiter().GetResult();

        if (!ok)
        {
            LogService.Write("ERROR: Extraction failed. Check the log for details.");
            return false;
        }

        LogService.Write("Extraction complete.");
        Dispatcher.Invoke(() => SetStatus("Post-processing...", 80));

        if (sdOnly)
        {
            LogService.Write("Deleting hd\\ folder (SD-only mode)...");
            ExtractionService.DeleteHdFolder(path);
            LogService.Write("hd\\ deleted.");
        }
        else
        {
            if (delHdFiles)
            {
                LogService.Write("Deleting .model / .texture files from hd\\...");
                ExtractionService.DeleteHdModelTextureFiles(path);
                LogService.Write("HD model/texture files deleted.");
            }
            if (delLobby)
            {
                LogService.Write("Deleting lobby UI folder...");
                ExtractionService.DeleteLobbyFolder(path);
                LogService.Write("Lobby folder deleted.");
            }
        }

        if (audioDegrade)
            LogService.Write("NOTE: Audio downgrade (ffmpeg) is not yet implemented — skipped.");

        if (!string.IsNullOrEmpty(gameVer))
        {
            ExtractionService.SaveVersionStamp(path, gameVer);
            LogService.Write($"Version stamp saved: {gameVer}");
        }

        Dispatcher.Invoke(() => SetStatus("Applying settings...", 90));
        DoApplySettingsImpl(path, launchArgs,
            vsync: true, fps: true, reduceGfx: reduceGfx, firewall: firewall);

        Dispatcher.Invoke(() => { SetProgress(100); SetStep(2, "done"); SetStep(3, "done"); });
        AudioService.Play("success");
        LogService.Write("=== Extract + Optimize complete ===");
        return true;
    }

    // ── Apply Settings ────────────────────────────────────────────────────────

    private void ApplyClick()
    {
        if (_opRunning) return;

        var path = txtPath.Text.Trim();
        if (string.IsNullOrEmpty(path)) return;

        AudioService.Play("press");

        bool vsync     = chkVSync.IsChecked       == true;
        bool fps       = chkFPSCap.IsChecked      == true;
        bool reduceGfx = chkReduceGfx.IsChecked   == true;
        bool firewall  = chkFirewall.IsChecked     == true;
        var  args      = BuildLaunchArgs();

        LogService.Write("=== Apply Settings started ===");

        try
        {
            DoApplySettingsImpl(path, args, vsync, fps, reduceGfx, firewall);
            SetStatus("Settings applied", null);
            AudioService.Play("success");
            LogService.Write("=== Apply Settings complete ===");
        }
        catch (Exception ex)
        {
            LogService.Write($"ERROR: {ex.Message}");
            AudioService.Play("error");
            MessageBox.Show(this, $"Error applying settings:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DoScan();
        }
    }

    private void DoApplySettingsImpl(string installPath, string launchArgs,
        bool vsync, bool fps, bool reduceGfx, bool firewall)
    {
        // Launch args (Battle.net.config)
        ConfigService.SetLaunchArgs(launchArgs);
        LogService.Write($"Launch args set: {(string.IsNullOrEmpty(launchArgs) ? "(none)" : launchArgs)}");

        // Settings.json
        var settings = new Dictionary<string, int>();
        if (vsync)     settings["VSync"]           = 0;
        if (fps)       settings["Framerate Cap"]   = 0;
        if (reduceGfx)
        {
            settings["Texture Quality"] = 0;
            settings["Shadow Quality"]  = 0;
            settings["Anti-Aliasing"]   = 0;
            settings["Light Quality"]   = 0;
        }
        if (settings.Count > 0)
        {
            ConfigService.SetSettings(settings);
            LogService.Write("Settings.json updated: " +
                string.Join(", ", settings.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        // Firewall
        if (firewall)
        {
            FirewallService.AddRule(Path.Combine(installPath, "D2R.exe"));
            LogService.Write("Firewall block rule added.");
        }
        else
        {
            FirewallService.RemoveRule();
            LogService.Write("Firewall block rule removed.");
        }
    }

    private string BuildLaunchArgs()
    {
        var parts = new List<string>();
        if (chkDirect.IsChecked  == true) parts.Add("-direct -txt");
        if (chkNoSound.IsChecked == true) parts.Add("-ns");
        if (chkRespec.IsChecked  == true) parts.Add("-enablerespec");

        int playerIdx = cmbPlayers.SelectedIndex;
        if (playerIdx > 0) parts.Add($"-players {playerIdx}");

        return string.Join(" ", parts);
    }

    // ── Revert ────────────────────────────────────────────────────────────────

    private void RevertClick()
    {
        if (_opRunning) return;

        var confirm = MessageBox.Show(this,
            "This will:\n" +
            "  \u2022 Clear all launch arguments (-direct, -txt, -ns, etc.)\n" +
            "  \u2022 Reset VSync and Framerate Cap to defaults\n" +
            "  \u2022 Remove the firewall block rule\n\n" +
            "Extracted files are NOT deleted (re-extract after game updates).\n\n" +
            "Continue?",
            "Revert Everything", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        AudioService.Play("press");
        LogService.Write("=== Revert started ===");

        try
        {
            ConfigService.SetLaunchArgs("");
            LogService.Write("Launch args cleared.");

            ConfigService.SetSettings(new Dictionary<string, int>
            {
                ["VSync"]         = 1,
                ["Framerate Cap"] = 200
            });
            LogService.Write("Settings.json reset (VSync=1, Framerate Cap=200).");

            FirewallService.RemoveRule();
            LogService.Write("Firewall rule removed.");

            SetStatus("Reverted", null);
            AudioService.Play("success");
            LogService.Write("=== Revert complete ===");
        }
        catch (Exception ex)
        {
            LogService.Write($"ERROR: {ex.Message}");
            AudioService.Play("error");
        }
        finally
        {
            DoScan();
        }
    }

    // ── Verify State ──────────────────────────────────────────────────────────

    private void VerifyClick()
    {
        AudioService.Play("scan");
        LogService.Write("=== Verify State ===");
        DoScan();
        if (_scan == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"D2R Path:     {_scan.InstallPath}");
        sb.AppendLine($"Game Version: {(_scan.GameVersion.Length > 0 ? _scan.GameVersion : "not found")}");
        sb.AppendLine($"Extracted:    {(_scan.IsExtracted ? "Yes" : "No")}");
        sb.AppendLine($"Ext. Version: {(_scan.IsExtracted ? _scan.ExtractedVersion : "N/A")}");
        sb.AppendLine($"Version OK:   {(_scan.VersionOk ? "Yes" : "MISMATCH")}");
        sb.AppendLine($"-direct:      {(_scan.DirectActive ? "Active" : "Inactive")}");
        sb.AppendLine($"VSync:        {(_scan.VsyncOff ? "Off (fast)" : "On")}");
        sb.AppendLine($"FPS Cap:      {(_scan.FpsCapOff ? "Uncapped" : "Capped (slows loads!)")}");
        sb.AppendLine($"Firewall:     {(_scan.FirewallBlock ? "Blocked" : "Open")}");
        sb.AppendLine($"Launch Args:  {ConfigService.GetLaunchArgs()}");

        bool allGood = _scan.IsExtracted && _scan.VersionOk && _scan.DirectActive &&
                       _scan.VsyncOff   && _scan.FpsCapOff;
        sb.AppendLine();
        sb.AppendLine(allGood
            ? "\u2713 All fast-load optimizations are active."
            : "\u26A0 Some optimizations are not yet active.");

        LogService.Write(sb.ToString());

        MessageBox.Show(this, sb.ToString(), "Verify State", MessageBoxButton.OK,
            allGood ? MessageBoxImage.Information : MessageBoxImage.Warning);

        if (allGood) SetStep(5, "done");
    }

    // ── RAM Disk (stub) ───────────────────────────────────────────────────────

    private void RamDiskClick()
    {
        MessageBox.Show(this,
            "RAM Disk setup creates a symlink from Data\\ to an imdisk RAM drive,\n" +
            "giving near-instant loading on systems with 32+ GB RAM.\n\n" +
            "This feature is planned for a future update.\n" +
            "Check the GitHub releases page for the latest version.",
            "RAM Disk Setup", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Quick Select ──────────────────────────────────────────────────────────

    private void SelectAll(bool value)
    {
        AudioService.Play("press");
        chkVSync.IsChecked         = value;
        chkFPSCap.IsChecked        = value;
        chkReduceGfx.IsChecked     = value;
        chkDirect.IsChecked        = value;
        chkNoSound.IsChecked       = value;
        chkRespec.IsChecked        = value;
        chkSDOnly.IsChecked        = value;
        chkDeleteHDFiles.IsChecked = value;
        chkDeleteLobby.IsChecked   = value;
        chkAudioDegrade.IsChecked  = value;
        chkFirewall.IsChecked      = value;
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void SetUiEnabled(bool enabled)
    {
        _opRunning = !enabled;
        bool hasD2R       = _scan != null && !string.IsNullOrEmpty(_scan.GameVersion);
        bool needsExtract = _scan == null || !_scan.IsExtracted || !_scan.VersionOk;

        btnScan.IsEnabled    = enabled;
        btnBrowse.IsEnabled  = enabled;
        btnLogs.IsEnabled    = enabled;
        btnRamDisk.IsEnabled = enabled;
        btnExtract.IsEnabled = enabled && hasD2R && needsExtract;
        btnApply.IsEnabled   = enabled && hasD2R;
        btnRevert.IsEnabled  = enabled && hasD2R;
        btnVerify.IsEnabled  = enabled && hasD2R;
    }

    private void StartOpTimer()
    {
        _opTimerSecs = 0;
        sbTimer.Text = "0:00";
        _opTimer.Start();
    }

    private void StopOpTimer()
    {
        _opTimer.Stop();
        sbTimer.Text = "";
    }

    private void SetStatus(string text, int? progress)
    {
        sbStatus.Text  = text;
        progBar.Value  = progress ?? 0;
    }

    private void SetProgress(int pct) => progBar.Value = pct;

    private void ToggleMute()
    {
        _muted              = !_muted;
        chkMuteOpt.IsChecked = _muted;
        AudioService.SetMuted(_muted);
        btnMute.Content     = _muted ? "\U0001F507" : "\u266A";
    }

    private void OpenLogFolder()
    {
        if (Directory.Exists(LogService.LogDir))
            System.Diagnostics.Process.Start("explorer.exe", LogService.LogDir);
    }

    private void AppendLog(string line)
    {
        txtLog.AppendText(line + Environment.NewLine);
        txtLog.ScrollToEnd();
    }
}
