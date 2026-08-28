using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace LogiBatteryWidget.App.Converters;

/// <summary>Green for a healthy charge level, red at 20% or below.</summary>
public sealed class BatteryFillColorConverter : IValueConverter
{
    private static readonly SolidColorBrush GoodBrush = new(Color.FromRgb(0x30, 0xD1, 0x58));
    private static readonly SolidColorBrush LowBrush = new(Color.FromRgb(0xFF, 0x3B, 0x30));

    static BatteryFillColorConverter()
    {
        GoodBrush.Freeze();
        LowBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var percentage = value as int?;
        return percentage is <= 20 ? LowBrush : GoodBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
