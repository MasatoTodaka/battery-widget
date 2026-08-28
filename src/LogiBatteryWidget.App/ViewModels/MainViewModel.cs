using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogiBatteryWidget.App.Settings;
using LogiBatteryWidget.Core;
using LogiBatteryWidget.Core.Models;

namespace LogiBatteryWidget.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly BatteryMonitorService _monitor;
    private readonly Dispatcher _dispatcher;

    private List<DevicePreference> _preferences = DeviceDisplaySettingsStore.Load();
    private IReadOnlyList<BatteryDevice> _latestDevices = [];

    [ObservableProperty]
    private bool _isRefreshing;

    public ObservableCollection<BatteryDeviceViewModel> Devices { get; } = [];

    public bool HasDevices => Devices.Count > 0;

    public bool IsEmpty => Devices.Count == 0;

    public MainViewModel(BatteryMonitorService monitor, Dispatcher dispatcher)
    {
        _monitor = monitor;
        _dispatcher = dispatcher;
        _monitor.DevicesUpdated += OnDevicesUpdated;
    }

    /// <summary>Snapshot for the settings window to edit. Includes devices not currently connected too.</summary>
    public IReadOnlyList<DevicePreference> GetPreferencesSnapshot() => _preferences;

    /// <summary>Called by the settings window whenever visibility/order changes - applies and persists immediately.</summary>
    public void UpdatePreferences(List<DevicePreference> preferences)
    {
        _preferences = preferences;
        DeviceDisplaySettingsStore.Save(_preferences);
        RebuildDisplayList();
    }

    private void OnDevicesUpdated(object? sender, IReadOnlyList<BatteryDevice> devices)
    {
        _dispatcher.Invoke(() =>
        {
            _latestDevices = devices;
            MergeNewlySeenDevices(devices);
            RebuildDisplayList();
        });
    }

    private void MergeNewlySeenDevices(IReadOnlyList<BatteryDevice> devices)
    {
        var knownKeys = new HashSet<string>(_preferences.Select(p => p.Key));
        var newOnes = devices.Where(d => !knownKeys.Contains(d.Key)).ToList();
        if (newOnes.Count == 0)
        {
            return;
        }

        _preferences = [.. _preferences, .. newOnes.Select(d => new DevicePreference(d.Key, d.Name, Visible: true))];
        DeviceDisplaySettingsStore.Save(_preferences);
    }

    private void RebuildDisplayList()
    {
        var devicesByKey = _latestDevices.ToDictionary(d => d.Key);

        Devices.Clear();
        foreach (var preference in _preferences)
        {
            if (!preference.Visible)
            {
                continue;
            }
            if (devicesByKey.TryGetValue(preference.Key, out var device))
            {
                Devices.Add(new BatteryDeviceViewModel(device, preference.Name));
            }
        }

        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Raised when the user picks a corner in the settings window. MainWindow owns the actual
    /// screen-geometry math and position persistence, so it just listens for this and acts.
    /// </summary>
    public event Action<WidgetCorner>? PositionChangeRequested;

    [RelayCommand]
    private void MoveToCorner(WidgetCorner corner) => PositionChangeRequested?.Invoke(corner);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            await _monitor.RefreshNowAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
