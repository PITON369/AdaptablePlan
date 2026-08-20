using System;
using System.Globalization;
using AdaptablePlan.Core.Models;
using Avalonia.Data.Converters;

namespace AdaptablePlan.UI.Converters;

public class TaskTypeDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            TaskType.Recurring => "Recurring",
            TaskType.FixedTime => "Fixed Time",
            TaskType.Things => "Things",
            TaskType.OneTime => "One Time (Things)",
            _ => value?.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
