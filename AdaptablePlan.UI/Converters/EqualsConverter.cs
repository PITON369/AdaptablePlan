using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AdaptablePlan.UI.Converters;

public class EqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.Equals(parameter) ?? false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? parameter : throw new InvalidOperationException();
    }
}
