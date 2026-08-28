using LogiBatteryWidget.Core.Models;

namespace LogiBatteryWidget.Core.Providers;

/// <summary>
/// A source of peripheral battery readings (a vendor tool's local API, a Windows built-in
/// enumeration, etc). Implementations must never throw for "not available right now" conditions
/// (vendor app not running, no matching devices) - they should just return an empty list so the
/// aggregator can keep polling other providers.
/// </summary>
public interface IBatteryProvider
{
    string SourceName { get; }

    Task<IReadOnlyList<BatteryDevice>> GetDevicesAsync(CancellationToken cancellationToken);
}
