using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using RTFreedom.Core.Enums;

namespace RTFreedom.App.Converters;

/// <summary>
/// Converts <see cref="PatchStatus"/> enumeration values to solid color brushes for UI indicators.
/// </summary>
public sealed class StatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush GrayBrush = new((Color)ColorConverter.ConvertFromString("#8A9BB5"));
    private static readonly SolidColorBrush CyanBrush = new((Color)ColorConverter.ConvertFromString("#00E5FF"));
    private static readonly SolidColorBrush GreenBrush = new((Color)ColorConverter.ConvertFromString("#00E676"));
    private static readonly SolidColorBrush AmberBrush = new((Color)ColorConverter.ConvertFromString("#FFD600"));
    private static readonly SolidColorBrush RedBrush = new((Color)ColorConverter.ConvertFromString("#FF1744"));
    private static readonly SolidColorBrush VioletBrush = new((Color)ColorConverter.ConvertFromString("#7C4DFF"));

    static StatusToColorConverter()
    {
        GrayBrush.Freeze();
        CyanBrush.Freeze();
        GreenBrush.Freeze();
        AmberBrush.Freeze();
        RedBrush.Freeze();
        VioletBrush.Freeze();
    }

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PatchStatus status)
        {
            return status switch
            {
                PatchStatus.NotAnalyzed => GrayBrush,
                PatchStatus.Analyzing => CyanBrush,
                PatchStatus.Compatible => GreenBrush,
                PatchStatus.PartiallyCompatible => AmberBrush,
                PatchStatus.Incompatible => RedBrush,
                PatchStatus.Patched => GreenBrush,
                PatchStatus.PatchFailed => RedBrush,
                PatchStatus.Restored => VioletBrush,
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
