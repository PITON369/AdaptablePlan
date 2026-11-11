using System;

namespace AdaptablePlan.UI.ViewModels;

// Текстовое поле времени "чч:мм": разбор для ввода с клавиатуры и обратно.
// TimeSpan.Parse не подходит: "7" он молча примет за 7 суток.
public static class TimeText
{
    public static TimeSpan? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var parts = text.Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var h) || h < 0 || h > 23
            || !int.TryParse(parts[1], out var m) || m < 0 || m > 59)
            return null;
        return new TimeSpan(h, m, 0);
    }

    public static string Format(TimeSpan? value)
        => value?.ToString(@"hh\:mm") ?? string.Empty;
}
