using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogiBatteryWidget.App.Settings;

namespace LogiBatteryWidget.App.ViewModels;

/// <summary>
/// Backs the settings window: which devices show in the widget, and in what order. Every change
/// (a checkbox, a move) applies to the live widget and saves immediately - there's no separate
/// Save/Cancel, since this is a small, low-stakes preference.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private bool _suppressApply;

    public ObservableCollection<DevicePreferenceRowViewModel> Rows { get; }

    /// <summary>Exposed so the corner-position buttons in the settings window can bind straight to it.</summary>
    public MainViewModel MainViewModel => _mainViewModel;

    public SettingsViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        Rows = [];
        _suppressApply = true;
        foreach (var preference in mainViewModel.GetPreferencesSnapshot())
        {
            AddRow(preference);
        }
        _suppressApply = false;
    }

    private void AddRow(DevicePreference preference)
    {
        var row = new DevicePreferenceRowViewModel(preference);
        row.PropertyChanged += OnRowPropertyChanged;
        Rows.Add(row);
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DevicePreferenceRowViewModel.Visible) or nameof(DevicePreferenceRowViewModel.Name))
        {
            Apply();
        }
    }

    [RelayCommand]
    private void MoveUp(DevicePreferenceRowViewModel row)
    {
        var index = Rows.IndexOf(row);
        if (index > 0)
        {
            Rows.Move(index, index - 1);
            Apply();
        }
    }

    [RelayCommand]
    private void MoveDown(DevicePreferenceRowViewModel row)
    {
        var index = Rows.IndexOf(row);
        if (index >= 0 && index < Rows.Count - 1)
        {
            Rows.Move(index, index + 1);
            Apply();
        }
    }

    private void Apply()
    {
        if (_suppressApply)
        {
            return;
        }

        _mainViewModel.UpdatePreferences(
            Rows.Select(r => new DevicePreference(r.Key, r.Name, r.Visible)).ToList());
    }
}
