using System;
using System.Globalization;
using AdaptablePlan.UI.Localization;
using Avalonia.Data.Converters;

namespace AdaptablePlan.UI.Converters;

// Настройки Theme/Language хранят служебные значения-строки ("System", ...),
// а показывать нужно локализованные подписи.
public class ThemeDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            "Light" => Strings.Get("ThemeLight"),
            "Dark" => Strings.Get("ThemeDark"),
            _ => Strings.Get("ThemeSystem"),
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class LanguageDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            "English" => Strings.Get("LangEnglish"),
            "Russian" => Strings.Get("LangRussian"),
            _ => Strings.Get("LangSystem"),
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
