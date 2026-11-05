using System;
using System.Globalization;
using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Localization;
using Avalonia.Data.Converters;

namespace AdaptablePlan.UI.Converters;

// Показ вида Custom-страницы в ComboBox: значения - enum, подпись - локализованная.
public class PageViewTypeDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            PageViewType.Day => Strings.Get("Day"),
            PageViewType.Week => Strings.Get("Week"),
            PageViewType.Month => Strings.Get("Month"),
            PageViewType.Year => Strings.Get("Year"),
            PageViewType.List => Strings.Get("List"),
            PageViewType.Note => Strings.Get("Note"),
            _ => value?.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
