using System.Windows;
using System.Windows.Input;
using LogiBatteryWidget.App.Settings;
using LogiBatteryWidget.App.ViewModels;

namespace LogiBatteryWidget.App;

public partial class MainWindow : Window
{
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
