namespace D2RFastLoad.Models;

public class ScanResult
{
    public string InstallPath { get; set; } = "";
    public bool IsExtracted   { get; set; }
    public bool VersionOk     { get; set; }
    public bool DirectActive  { get; set; }
    public bool VsyncOff      { get; set; }
    public bool FpsCapOff     { get; set; }
    public bool FirewallBlock { get; set; }
    public string GameVersion { get; set; } = "";
    public string ExtractedVersion { get; set; } = "";
}
