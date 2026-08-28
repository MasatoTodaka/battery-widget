using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogiBatteryWidget.Core;

namespace LogiBatteryWidget.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly BatteryMonitorService _monitor;
    private readonly Dispatcher _dispatcher;

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

    private void OnDevicesUpdated(object? sender, IReadOnlyList<Core.Models.BatteryDevice> devices)
    {
        _dispatcher.Invoke(() =>
        {
            Devices.Clear();
            foreach (var device in devices.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Devices.Add(new BatteryDeviceViewModel(device));
            }
            OnPropertyChanged(nameof(HasDevices));
            OnPropertyChanged(nameof(IsEmpty));
        });
    }

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
