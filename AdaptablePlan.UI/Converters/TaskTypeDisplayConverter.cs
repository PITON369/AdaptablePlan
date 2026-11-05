using System;
using System.Globalization;
using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Localization;
using Avalonia.Data.Converters;

namespace AdaptablePlan.UI.Converters;

public class TaskTypeDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            TaskType.Recurring => Strings.Get("TypeRecurring"),
            TaskType.FixedTime => Strings.Get("TypeFixedTime"),
            TaskType.Things => Strings.Get("TypeThings"),
            TaskType.OneTime => Strings.Get("TypeOneTime"),
            _ => value?.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
