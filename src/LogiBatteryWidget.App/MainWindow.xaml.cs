using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using LogiBatteryWidget.App.Settings;
using LogiBatteryWidget.App.ViewModels;

namespace LogiBatteryWidget.App;

public partial class MainWindow : Window
{
    // DWM_WINDOW_CORNER_PREFERENCE - gives the window itself rounded corners at the compositor
    // level (Windows 11+). Used instead of AllowsTransparency: layered (per-pixel-alpha) windows
    // are known to fail to composite correctly on some remote/virtual display setups, where they
    // end up not drawing anything at all despite the process running fine.
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        var savedPosition = WidgetPositionStore.Load();
        if (savedPosition is not null)
        {
            Left = savedPosition.Left;
            Top = savedPosition.Top;
        }
        else
        {
            // Default: near the top-right corner, echoing where iOS/macOS widgets tend to sit.
            Loaded += (_, _) =>
            {
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 24;
                Top = workArea.Top + 24;
            };
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var preference = DwmwcpRound;
        try
        {
            DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Pre-Windows 11: no native corner rounding API. The window just stays square-cornered.
        }
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        WidgetPositionStore.Save(new WidgetPosition(Left, Top));
    }
}
