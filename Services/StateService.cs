using System.IO;
using System.Text.Json;
using D2RFastLoad.Models;

namespace D2RFastLoad.Services;

public static class StateService
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "D2RFastLoad", "state.json");

    public static AppState Load()
    {
        try
        {
            if (!File.Exists(StatePath)) return new AppState();
            return JsonSerializer.Deserialize<AppState>(File.ReadAllText(StatePath)) ?? new AppState();
        }
        catch { return new AppState(); }
    }

    public static void Save(AppState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath,
                JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
