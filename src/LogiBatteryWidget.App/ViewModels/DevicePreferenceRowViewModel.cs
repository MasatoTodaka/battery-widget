using CommunityToolkit.Mvvm.ComponentModel;
using LogiBatteryWidget.App.Settings;

namespace LogiBatteryWidget.App.ViewModels;

public sealed partial class DevicePreferenceRowViewModel(DevicePreference preference) : ObservableObject
{
    public string Key { get; } = preference.Key;

    [ObservableProperty]
    private string _name = preference.Name;

    [ObservableProperty]
    private bool _visible = preference.Visible;
}
