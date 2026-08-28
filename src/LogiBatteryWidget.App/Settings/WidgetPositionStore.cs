using System.IO;
using System.Text.Json;

namespace LogiBatteryWidget.App.Settings;

public sealed record WidgetPosition(double Left, double Top);

/// <summary>Remembers where the user last dragged the floating widget, across app restarts.</summary>
public static class WidgetPositionStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LogiBatteryWidget",
        "window-position.json");

    public static WidgetPosition? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<WidgetPosition>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(WidgetPosition position)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(position));
        }
        catch
        {
            // Losing the remembered position isn't worth surfacing to the user.
        }
    }
}
