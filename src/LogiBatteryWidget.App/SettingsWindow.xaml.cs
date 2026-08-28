using System.Windows;
using LogiBatteryWidget.App.ViewModels;

namespace LogiBatteryWidget.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(mainViewModel);
    }
}
