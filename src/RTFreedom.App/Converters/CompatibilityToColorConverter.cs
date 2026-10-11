using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using RTFreedom.Core.Enums;

namespace RTFreedom.App.Converters;

/// <summary>
/// Converts <see cref="CompatibilityLevel"/> values to representative brush colors.
/// </summary>
public sealed class CompatibilityToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush GrayBrush = new((Color)ColorConverter.ConvertFromString("#8A9BB5"));
    private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#00E676"));
    private static readonly SolidColorBrush YellowBrush = new((Color)ColorConverter.ConvertFromString("#FFD600"));
    private static readonly SolidColorBrush OrangeBrush = new((Color)ColorConverter.ConvertFromString("#FF9100"));
    private static readonly SolidColorBrush RedBrush = new((Color)ColorConverter.ConvertFromString("#FF1744"));

    static CompatibilityToColorConverter()
    {
        GrayBrush.Freeze();
        GreenBrush.Freeze();
        YellowBrush.Freeze();
        OrangeBrush.Freeze();
        RedBrush.Freeze();
    }

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CompatibilityLevel level)
        {
            return level switch
            {
                CompatibilityLevel.FullyCompatible => GreenBrush,
                CompatibilityLevel.PartiallyCompatible => YellowBrush,
                CompatibilityLevel.ExperimentalOnly => OrangeBrush,
                CompatibilityLevel.Incompatible => RedBrush,
                CompatibilityLevel.Unknown => GrayBrush,
                _ => GrayBrush
            };
        }

        return GrayBrush;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
