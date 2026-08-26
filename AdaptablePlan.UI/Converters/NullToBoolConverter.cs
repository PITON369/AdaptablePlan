using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AdaptablePlan.UI.Converters;

public class NullToBoolConverter : IValueConverter
{
    public static NullToBoolConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NullToBoolInverseConverter : IValueConverter
{
    public static NullToBoolInverseConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value == null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
