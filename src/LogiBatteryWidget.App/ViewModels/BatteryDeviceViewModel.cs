using LogiBatteryWidget.Core.Models;

namespace LogiBatteryWidget.App.ViewModels;

public sealed class BatteryDeviceViewModel(BatteryDevice device)
{
    public string Name { get; } = device.Name;

    public string SourceName { get; } = device.Source;

    public int? Percentage { get; } = device.Percentage;

    /// <summary>Non-nullable convenience for bindings that need a plain number (e.g. the ring gauge).</summary>
    public double RingPercentage => Percentage ?? 0;

    public bool IsCharging { get; } = device.IsCharging;

    public bool IsUnknown => Percentage is null;

    public string PercentageText => Percentage is { } value ? $"{value}%" : "--";

    public string Glyph => device.Kind switch
    {
        BatteryDeviceKind.Mouse => "\U0001F5B1",
        BatteryDeviceKind.Keyboard => "⌨",
        BatteryDeviceKind.Headset => "\U0001F3A7",
        BatteryDeviceKind.Speaker => "\U0001F50A",
        _ => "\U0001F50C",
    };
}
