using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AdaptablePlan.UI.Converters;

public class DaysOfWeekConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is HashSet<DayOfWeek> days && days.Count > 0)
        {
            var abbrevs = new[] { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
            var parts = new List<string>();
            for (int i = 0; i < 7; i++)
                if (days.Contains((DayOfWeek)i))
                    parts.Add(abbrevs[i]);
            return string.Join(", ", parts);
        }
        return "-";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
