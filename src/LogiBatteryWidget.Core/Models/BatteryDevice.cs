namespace LogiBatteryWidget.Core.Models;

/// <summary>
/// One physical peripheral's battery reading, as reported by a single <see cref="Providers.IBatteryProvider"/>.
/// </summary>
/// <param name="Id">Stable identity within its source (e.g. G HUB's internal device id, or the Windows device instance id).</param>
/// <param name="Source">Which provider reported this (e.g. "Logitech G HUB", "Windows").</param>
public sealed record BatteryDevice(
    string Id,
    string Source,
    string Name,
    BatteryDeviceKind Kind,
    int? Percentage,
    bool IsCharging)
{
    /// <summary>Globally unique key across all providers, used for de-duplication and UI list identity.</summary>
    public string Key => $"{Source}:{Id}";
}
