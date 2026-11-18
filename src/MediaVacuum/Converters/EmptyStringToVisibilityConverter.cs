using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MediaVacuum.Converters;

/// <summary>Visible when the bound string is null/empty (used for placeholders).</summary>
public class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var empty = string.IsNullOrWhiteSpace(value as string);
        var invert = parameter?.ToString() == "invert";
        return empty ^ invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
