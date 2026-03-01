using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace D2RFastLoad.Services;

public static class ConfigService
{
    public static string BnetConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Battle.net", "Battle.net.config");

    public static string D2RSettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     "Saved Games", "Diablo II Resurrected", "Settings.json");

    // ── Battle.net.config ─────────────────────────────────────────────────────

    public static string GetLaunchArgs()
    {
        try
        {
            var raw  = File.ReadAllText(BnetConfigPath);
            var root = JsonNode.Parse(raw);
            return root?["Games"]?["OSI"]?["AdditionalLaunchArguments"]?.GetValue<string>() ?? "";
        }
        catch { return ""; }
    }

    public static void SetLaunchArgs(string args)
    {
        var raw  = File.ReadAllText(BnetConfigPath);
        var root = JsonNode.Parse(raw) ?? new JsonObject();

        root["Games"] ??= new JsonObject();
        root["Games"]!["OSI"] ??= new JsonObject();
        root["Games"]!["OSI"]!["AdditionalLaunchArguments"] = args;

        File.WriteAllText(BnetConfigPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool BnetConfigValid()
    {
        try { JsonNode.Parse(File.ReadAllText(BnetConfigPath)); return true; }
        catch { return false; }
    }

    // ── Settings.json ──────────────────────────────────────────────────────────

    public static int GetSetting(string key)
    {
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(D2RSettingsPath));
            return root?[key]?.GetValue<int>() ?? -1;
        }
        catch { return -1; }
    }

    public static void SetSetting(string key, int value)
    {
        var raw  = File.ReadAllText(D2RSettingsPath);
        var root = JsonNode.Parse(raw) ?? new JsonObject();
        root[key] = value;
        File.WriteAllText(D2RSettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void SetSettings(Dictionary<string, int> values)
    {
        var raw  = File.ReadAllText(D2RSettingsPath);
        var root = JsonNode.Parse(raw) ?? new JsonObject();
        foreach (var kv in values) root[kv.Key] = kv.Value;
        File.WriteAllText(D2RSettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool D2RSettingsValid()
    {
        try { JsonNode.Parse(File.ReadAllText(D2RSettingsPath)); return true; }
        catch { return false; }
    }
}
