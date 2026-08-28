using System.Diagnostics;
using LogiBatteryWidget.Core.Models;
using LogiBatteryWidget.Core.Providers;

namespace LogiBatteryWidget.Core;

/// <summary>
/// Polls every configured <see cref="IBatteryProvider"/> on an interval and raises
/// <see cref="DevicesUpdated"/> with the merged, latest reading from all of them.
/// </summary>
public sealed class BatteryMonitorService : IAsyncDisposable
{
    private readonly IReadOnlyList<IBatteryProvider> _providers;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _pollLoop;

    public BatteryMonitorService(IReadOnlyList<IBatteryProvider> providers, TimeSpan? pollInterval = null)
    {
        _providers = providers;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(60);
    }

    public event EventHandler<IReadOnlyList<BatteryDevice>>? DevicesUpdated;

    public void Start()
    {
        if (_pollLoop is not null)
        {
            return;
        }

        _pollLoop = RunAsync(_cts.Token);
    }

    /// <summary>Runs one poll immediately, outside the regular interval (e.g. for a manual refresh button).</summary>
    public async Task RefreshNowAsync()
    {
        var devices = await PollAllProvidersAsync(_cts.Token).ConfigureAwait(false);
        DevicesUpdated?.Invoke(this, devices);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);
        do
        {
            var devices = await PollAllProvidersAsync(cancellationToken).ConfigureAwait(false);
            DevicesUpdated?.Invoke(this, devices);
        } while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<IReadOnlyList<BatteryDevice>> PollAllProvidersAsync(CancellationToken cancellationToken)
    {
        var results = new List<BatteryDevice>();

        foreach (var provider in _providers)
        {
            try
            {
                var devices = await provider.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
                results.AddRange(devices);
            }
            catch (Exception ex)
            {
                // A single misbehaving provider must not take down monitoring for the rest.
                Debug.WriteLine($"[BatteryMonitorService] provider '{provider.SourceName}' threw: {ex}");
            }
        }

        return results;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_pollLoop is not null)
        {
            try
            {
                await _pollLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }
        _cts.Dispose();
    }
}
