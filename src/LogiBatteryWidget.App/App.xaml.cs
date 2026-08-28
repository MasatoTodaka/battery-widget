using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using LogiBatteryWidget.App.Settings;
using LogiBatteryWidget.App.ViewModels;
using LogiBatteryWidget.Core;
using LogiBatteryWidget.Core.Providers;

namespace LogiBatteryWidget.App;

public partial class App : Application
{
    private BatteryMonitorService? _monitor;
    private TaskbarIcon? _taskbarIcon;
    private MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IReadOnlyList<IBatteryProvider> providers =
        [
            new GHubBatteryProvider(),
            new WindowsBatteryProvider(),
        ];

        _monitor = new BatteryMonitorService(providers, TimeSpan.FromSeconds(45));
        var viewModel = new MainViewModel(_monitor, Dispatcher);

        _mainWindow = new MainWindow(viewModel);
        _mainWindow.Show();

        _taskbarIcon = BuildTaskbarIcon(viewModel);

        _monitor.Start();
        await _monitor.RefreshNowAsync();
    }

    private TaskbarIcon BuildTaskbarIcon(MainViewModel viewModel)
    {
        var toggleVisibilityItem = new MenuItem { Header = "ウィジェットを表示/非表示" };
        toggleVisibilityItem.Click += (_, _) => ToggleWidgetVisibility();

        var refreshItem = new MenuItem { Header = "今すぐ更新" };
        refreshItem.Click += async (_, _) => await viewModel.RefreshCommand.ExecuteAsync(null);

        var exitItem = new MenuItem { Header = "終了" };
        exitItem.Click += (_, _) => Shutdown();

        var contextMenu = new ContextMenu
        {
            ItemsSource = new object[] { toggleVisibilityItem, refreshItem, new Separator(), exitItem },
        };

        var icon = new TaskbarIcon
        {
            Icon = TrayIconFactory.CreateBatteryIcon(),
            ToolTipText = "Logi Battery Widget",
            ContextMenu = contextMenu,
        };
        icon.TrayMouseDoubleClick += (_, _) => ToggleWidgetVisibility();
        icon.ForceCreate();
        return icon;
    }

    private void ToggleWidgetVisibility()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Visibility = _mainWindow.Visibility == Visibility.Visible
            ? Visibility.Hidden
            : Visibility.Visible;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _taskbarIcon?.Dispose();
        _monitor?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.OnExit(e);
    }
}
