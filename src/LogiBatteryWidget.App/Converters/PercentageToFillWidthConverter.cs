using System.Globalization;
using System.Windows.Data;

namespace LogiBatteryWidget.App.Converters;

/// <summary>Maps a 0-100 percentage to a pixel width, given the full bar width as the converter parameter.</summary>
public sealed class PercentageToFillWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fullWidth = parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
            ? w
            : 0.0;

        var percentage = Math.Clamp(value as int? ?? 0, 0, 100);
        return fullWidth * percentage / 100.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
