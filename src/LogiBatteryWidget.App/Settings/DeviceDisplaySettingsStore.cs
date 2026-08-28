using System.IO;
using System.Text.Json;

namespace LogiBatteryWidget.App.Settings;

/// <summary>Remembers which devices the user wants shown, and in what order, across restarts.</summary>
public static class DeviceDisplaySettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LogiBatteryWidget",
        "device-preferences.json");

    public static List<DevicePreference> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<DevicePreference>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IReadOnlyList<DevicePreference> preferences)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(preferences));
        }
        catch
        {
            // Losing a display preference isn't worth surfacing to the user.
        }
    }
}
