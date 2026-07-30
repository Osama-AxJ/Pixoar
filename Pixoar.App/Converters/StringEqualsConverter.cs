using System.Globalization;
using System.Windows.Data;

namespace Pixoar.App.Converters;

/// <summary>
/// Converts string equality into a boolean value for option controls.
/// </summary>
public sealed class StringEqualsConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true && parameter is string text ? text : Binding.DoNothing;
    }
}
