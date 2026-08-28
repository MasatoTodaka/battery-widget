using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using LogiBatteryWidget.App.Settings;
using LogiBatteryWidget.App.ViewModels;
using LogiBatteryWidget.Core;
using LogiBatteryWidget.Core.Providers;
using LogiBatteryWidget.Core.Providers.Inzone;
using LogiBatteryWidget.Core.Providers.Pulsar;
using LogiBatteryWidget.Core.Providers.Vaxee;

namespace LogiBatteryWidget.App;

public partial class App : Application
{
    private BatteryMonitorService? _monitor;
    private TaskbarIcon? _taskbarIcon;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IReadOnlyList<IBatteryProvider> providers =
        [
            new GHubBatteryProvider(),
            new WindowsBatteryProvider(),
            new InzoneBatteryProvider(),
            new VaxeeBatteryProvider(),
            new PulsarBatteryProvider(),
        ];

        _monitor = new BatteryMonitorService(providers, TimeSpan.FromSeconds(45));
        var viewModel = new MainViewModel(_monitor, Dispatcher);

        _mainWindow = new MainWindow(viewModel);
        _mainWindow.Show();

        _taskbarIcon = BuildTaskbarIcon(viewModel);

        // Start() already polls immediately (its loop is do/while) before waiting on the timer,
        // so there's no separate initial RefreshNowAsync() call here - calling both raced two
        // concurrent polls against the same providers, and whichever finished last (sometimes a
        // partial/slower read) silently won.
        _monitor.Start();
    }

    private TaskbarIcon BuildTaskbarIcon(MainViewModel viewModel)
    {
        var toggleVisibilityItem = new MenuItem { Header = "ウィジェットを表示/非表示" };
        toggleVisibilityItem.Click += (_, _) => ToggleWidgetVisibility();

        var refreshItem = new MenuItem { Header = "今すぐ更新" };
        refreshItem.Click += async (_, _) => await viewModel.RefreshCommand.ExecuteAsync(null);

        var settingsItem = new MenuItem { Header = "設定..." };
        settingsItem.Click += (_, _) => OpenSettings(viewModel);

        var exitItem = new MenuItem { Header = "終了" };
        exitItem.Click += (_, _) => Shutdown();

        var contextMenu = new ContextMenu
        {
            ItemsSource = new object[] { toggleVisibilityItem, refreshItem, settingsItem, new Separator(), exitItem },
        };

        var icon = new TaskbarIcon
        {
            Icon = TrayIconFactory.CreateBatteryIcon(),
            ToolTipText = "Battery Widget",
            ContextMenu = contextMenu,
        };
        icon.TrayMouseDoubleClick += (_, _) => ToggleWidgetVisibility();
        icon.ForceCreate();
        return icon;
    }

    private void OpenSettings(MainViewModel viewModel)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(viewModel);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
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
