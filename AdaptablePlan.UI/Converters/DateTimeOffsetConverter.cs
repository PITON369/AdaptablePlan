using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AdaptablePlan.UI.Converters;

// DatePicker.SelectedDate - DateTimeOffset?, в модели дата хранится как DateTime?.
public class DateTimeOffsetConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime d ? new DateTimeOffset(d) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTimeOffset d ? d.DateTime : null;
}
