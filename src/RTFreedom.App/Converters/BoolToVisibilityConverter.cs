using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RTFreedom.App.Converters;

/// <summary>
/// Converts boolean values to WPF <see cref="Visibility"/> enumerations.
/// Supports inversion when parameter is set to "inverse" or "true".
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets whether false should map to Hidden instead of Collapsed.
    /// </summary>
    public bool UseHidden { get; set; }

    /// <summary>
    /// Gets or sets whether the conversion logic should be inverted.
    /// </summary>
    public bool Invert { get; set; }

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolVal = value is true;

        bool invert = Invert;
        if (parameter is string paramStr && bool.TryParse(paramStr, out var parsedInvert))
        {
            invert = parsedInvert;
        }

        if (invert)
        {
            boolVal = !boolVal;
        }

        if (boolVal)
        {
            return Visibility.Visible;
        }

        return UseHidden ? Visibility.Hidden : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            bool boolVal = visibility == Visibility.Visible;
            return Invert ? !boolVal : boolVal;
        }

        return false;
    }
}
