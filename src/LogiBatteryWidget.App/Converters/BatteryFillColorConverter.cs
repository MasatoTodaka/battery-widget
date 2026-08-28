using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LogiBatteryWidget.App.Converters;

/// <summary>
/// Mimics iOS/macOS battery coloring: green while charging, red at low charge, otherwise a
/// neutral fill. Bindings: [0] Percentage (int?), [1] IsCharging (bool).
/// </summary>
public sealed class BatteryFillColorConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush ChargingBrush = new(Color.FromRgb(0x30, 0xD1, 0x58));
    private static readonly SolidColorBrush LowBrush = new(Color.FromRgb(0xFF, 0x3B, 0x30));
    private static readonly SolidColorBrush NormalBrush = new(Color.FromRgb(0xE8, 0xE8, 0xEA));

    static BatteryFillColorConverter()
    {
        ChargingBrush.Freeze();
        LowBrush.Freeze();
        NormalBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var percentage = values.ElementAtOrDefault(0) as int?;
        var isCharging = values.ElementAtOrDefault(1) as bool? ?? false;

        if (isCharging)
        {
            return ChargingBrush;
        }

        if (percentage is <= 20)
        {
            return LowBrush;
        }

        return NormalBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
