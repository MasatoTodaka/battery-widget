using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using LogiBatteryWidget.Core.Models;
using Windows.Devices.Enumeration;
using Windows.Devices.Power;
using Windows.System.Power;

namespace LogiBatteryWidget.Core.Providers;

/// <summary>
/// Vendor-agnostic fallback: reads whatever battery-capable devices Windows itself already
/// knows about (mainly Bluetooth peripherals using the standard GATT Battery Service, which is
/// how the "Bluetooth &amp; devices" battery percentages are populated). This has no dependency on
/// any vendor tool, so it's the only source that can pick up non-Logitech peripherals - but it
/// only works for devices connected in a way Windows itself tracks battery for (typically
/// Bluetooth). Peripherals connected via a proprietary 2.4GHz dongle (e.g. Logitech Lightspeed)
/// are invisible here even if the vendor's own software can read their battery.
/// </summary>
public sealed class WindowsBatteryProvider : IBatteryProvider
{
    // Peripheral batteries are a few hundred to a few thousand mWh. A laptop's internal battery
    // is tens of thousands. Used to keep the host machine's own battery out of this list - it's
    // not a "peripheral" and the OS already shows it everywhere.
    private const int InternalBatteryCapacityThresholdMilliwattHours = 10_000;

    public string SourceName => "Windows";

    public async Task<IReadOnlyList<BatteryDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var selector = Battery.GetDeviceSelector();
            var deviceInfos = await DeviceInformation.FindAllAsync(selector).AsTask(cancellationToken).ConfigureAwait(false);

            string? aggregateDeviceId = null;
            try
            {
                aggregateDeviceId = Battery.AggregateBattery.DeviceId;
            }
            catch
            {
                // no aggregate battery available (e.g. desktop with no internal battery) - fine.
            }

            var results = new List<BatteryDevice>();
            foreach (var deviceInfo in deviceInfos)
            {
                if (aggregateDeviceId is not null && string.Equals(deviceInfo.Id, aggregateDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                Battery battery;
                try
                {
                    battery = await Battery.FromIdAsync(deviceInfo.Id).AsTask(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WindowsBatteryProvider] Battery.FromIdAsync failed for {deviceInfo.Id}: {ex.Message}");
                    continue;
                }

                var report = battery.GetReport();
                var fullCapacity = report.FullChargeCapacityInMilliwattHours;
                var remainingCapacity = report.RemainingCapacityInMilliwattHours;

                if (fullCapacity is > InternalBatteryCapacityThresholdMilliwattHours)
                {
                    continue; // looks like the host machine's own battery, not a peripheral
                }

                int? percentage = fullCapacity is > 0 && remainingCapacity is not null
                    ? (int)Math.Round(remainingCapacity.Value * 100.0 / fullCapacity.Value)
                    : null;

                var name = string.IsNullOrWhiteSpace(deviceInfo.Name) ? deviceInfo.Id : deviceInfo.Name;

                results.Add(new BatteryDevice(
                    Id: deviceInfo.Id,
                    Source: SourceName,
                    Name: name,
                    Kind: GuessKind(name),
                    Percentage: percentage,
                    IsCharging: report.Status == BatteryStatus.Charging));
            }

            return results;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsBatteryProvider] enumeration failed: {ex.Message}");
            return [];
        }
    }

    private static BatteryDeviceKind GuessKind(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("mouse")) return BatteryDeviceKind.Mouse;
        if (lower.Contains("keyboard")) return BatteryDeviceKind.Keyboard;
        if (lower.Contains("headset") || lower.Contains("headphone") || lower.Contains("earbud")) return BatteryDeviceKind.Headset;
        if (lower.Contains("speaker")) return BatteryDeviceKind.Speaker;
        return BatteryDeviceKind.Other;
    }
}
