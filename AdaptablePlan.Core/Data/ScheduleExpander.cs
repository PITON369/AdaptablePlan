using System;
using System.Collections.Generic;
using System.Linq;
using AdaptablePlan.Core.Models;

namespace AdaptablePlan.Core.Data;

// Разворот повторяющихся шаблонов в пункты на конкретные даты.
// Чистые функции без состояния — используются VM и проверкой конфликтов.
public static class ScheduleExpander
{
    // Якорь чётности недель для WeekInterval > 1 (понедельник).
    private static readonly DateTime WeekAnchor = new(2024, 1, 1);

    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Trim().Split(':');
        if (parts.Length != 2)
            return false;
        if (!int.TryParse(parts[0], out var hours) || !int.TryParse(parts[1], out var minutes))
            return false;
        if (hours is < 0 or > 23 || minutes is < 0 or > 59)
            return false;
        time = new TimeSpan(hours, minutes, 0);
        return true;
    }

    // Интервал варианта: конец = EndTime либо Start + DurationMinutes.
    public static (TimeSpan? Start, TimeSpan? End) VariantInterval(TaskTemplate t, TaskTimeVariant v)
    {
        if (!TryParseTime(v.StartTime, out var start))
            return (null, null);
        var end = TryParseTime(v.EndTime, out var e) ? (TimeSpan?)e
            : t.DurationMinutes > 0 ? start + TimeSpan.FromMinutes(t.DurationMinutes) : null;
        if (end == null)
            return (start, null);
        var en = end.Value;
        if (en <= start)
            en = en.Add(TimeSpan.FromHours(24));
        return (start, en);
    }

    // Вариант, действующий на заданный день (нет — задача в этот день не выполняется).
    public static TaskTimeVariant? VariantForDay(TaskTemplate t, DayOfWeek day)
        => t.DayTimes.FirstOrDefault(v => v.Days.Contains(day));

    public static (TimeSpan? Start, TimeSpan? End) IntervalForDay(TaskTemplate t, DayOfWeek day)
    {
        var v = VariantForDay(t, day);
        return v == null ? (null, null) : VariantInterval(t, v);
    }

    public static string FormatTime(TimeSpan t)
        => $"{(int)t.TotalHours % 24:00}:{t.Minutes:00}";

    private static RecurrenceKind EffectiveKind(TaskTemplate t)
        => t.Type == TaskType.OneTime ? RecurrenceKind.Once : t.Kind;

    // Самый ранний по началу вариант — время для Monthly/Yearly/Once и для
    // дней без подходящего варианта у Daily.
    private static TaskTimeVariant? EarliestVariant(TaskTemplate t)
        => t.DayTimes
            .Where(v => TryParseTime(v.StartTime, out _))
            .OrderBy(v => TryParseTime(v.StartTime, out var ts) ? ts : TimeSpan.MaxValue)
            .FirstOrDefault();

    private static DateTime MondayOf(DateTime d)
        => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    // Разворот шаблона в пункты на дни диапазона [firstDay, firstDay+dayCount).
    // Пункт без времени — Start/End пустые строки. dayOffset применяется только
    // к своей дате и только к задачам без фиксированного времени.
    public static List<(DateTime Date, string Start, string End)> Expand(TaskTemplate t, DateTime firstDay, int dayCount, DayOffset? dayOffset = null)
    {
        var result = new List<(DateTime, string, string)>();
        var kind = EffectiveKind(t);
        var anchor = t.StartDate ?? t.Date;

        for (int i = 0; i < dayCount; i++)
        {
            var date = firstDay.AddDays(i);
            if (t.StartDate is DateTime sd && date < sd.Date)
                continue;
            if (t.EndDate is DateTime ed && date > ed.Date)
                continue;

            var occurs = kind switch
            {
                RecurrenceKind.Once => t.Date is DateTime d && d.Date == date,
                RecurrenceKind.Daily => true,
                RecurrenceKind.Weekly => VariantForDay(t, date.DayOfWeek) != null
                    && Mod(((MondayOf(date) - MondayOf(anchor ?? WeekAnchor)).Days) / 7, Math.Max(1, t.WeekInterval)) == 0,
                RecurrenceKind.Monthly => anchor is DateTime am && date >= am.Date && date.Day == am.Day,
                RecurrenceKind.Yearly => anchor is DateTime ay && date >= ay.Date
                    && date.Month == ay.Month && date.Day == ay.Day,
                _ => false,
            };
            if (!occurs)
                continue;

            var variant = kind == RecurrenceKind.Weekly
                ? VariantForDay(t, date.DayOfWeek)
                : VariantForDay(t, date.DayOfWeek) ?? EarliestVariant(t);
            string start = string.Empty, end = string.Empty;
            if (variant != null)
            {
                var (s, e) = VariantInterval(t, variant);
                if (s != null)
                    start = FormatTime(Normalize(Shift(s.Value, date, t, dayOffset)));
                if (e != null)
                    end = FormatTime(Normalize(Shift(e.Value, date, t, dayOffset)));
            }
            result.Add((date, start, end));
        }
        return result;
    }

    private static TimeSpan Shift(TimeSpan time, DateTime date, TaskTemplate t, DayOffset? dayOffset)
        => dayOffset != null && dayOffset.Date == date.ToString("yyyy-MM-dd") && t.Type != TaskType.FixedTime
            ? time + TimeSpan.FromMinutes(dayOffset.OffsetMinutes)
            : time;

    // Сдвиг может увести время в предыдущие сутки — тогда показываем его
    // как время прошедшей ночи (23:xx), а не отрицательное.
    private static TimeSpan Normalize(TimeSpan t)
        => t < TimeSpan.Zero ? t.Add(TimeSpan.FromHours(24)) : t;

    private static int Mod(int x, int m)
        => ((x % m) + m) % m;
}
