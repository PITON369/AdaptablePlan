using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;

namespace AdaptablePlan.UI.ViewModels;

// Расписание: выбранный режим/дата, разворот шаблонов в даты (ScheduleExpander),
// вьюхи Day/Week и недельная сетка на реальных датах.
public partial class MainWindowViewModel
{
    [ObservableProperty]
    private ScheduleMode _mode = ScheduleMode.Day;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private int _selectedCustomSlot;

    // Радио-кнопки режимов: двухсторонний биндинг на bool-свойства.
    public bool IsDayMode
    {
        get => Mode == ScheduleMode.Day;
        set { if (value) Mode = ScheduleMode.Day; }
    }

    public bool IsWeekMode
    {
        get => Mode == ScheduleMode.Week;
        set { if (value) Mode = ScheduleMode.Week; }
    }

    public bool IsMonthMode
    {
        get => Mode == ScheduleMode.Month;
        set { if (value) Mode = ScheduleMode.Month; }
    }

    public bool IsYearMode
    {
        get => Mode == ScheduleMode.Year;
        set { if (value) Mode = ScheduleMode.Year; }
    }

    public bool IsCustomMode
    {
        get => Mode == ScheduleMode.Custom;
        set { if (value) Mode = ScheduleMode.Custom; }
    }

    public string SelectedDateText
        => Mode is ScheduleMode.Day or ScheduleMode.Week or ScheduleMode.Month
            ? SelectedDate.ToString("ddd d MMM yyyy", CultureInfo.CurrentCulture)
            : string.Empty;

    partial void OnModeChanged(ScheduleMode value)
    {
        OnPropertyChanged(nameof(IsDayMode));
        OnPropertyChanged(nameof(IsWeekMode));
        OnPropertyChanged(nameof(IsMonthMode));
        OnPropertyChanged(nameof(IsYearMode));
        OnPropertyChanged(nameof(IsCustomMode));
        OnPropertyChanged(nameof(SelectedDateText));
        RegenerateSchedule();
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(SelectedDateText));
        RegenerateSchedule();
        OnPropertyChanged(nameof(CustomConflictText));
    }

    partial void OnStartWeekOnMondayChanged(bool value) => RegenerateSchedule();

    private ScheduleItem? _lastSelectedItem;

    // Порядок колонок сетки (индекс ячейки для каждой даты) — нужен для
    // обновления столбца Time при выборе таски.
    private Dictionary<DateTime, int>? _dayCellIndex;

    partial void OnSelectedItemChanged(ScheduleItem? value)
    {
        if (!ReferenceEquals(_lastSelectedItem, value))
        {
            if (_lastSelectedItem != null)
                _lastSelectedItem.IsSelected = false;
            _lastSelectedItem = value;
            if (value != null)
                value.IsSelected = true;
        }
        SelectedTask = value?.Template;
        SelectedTimeText = value == null || string.IsNullOrEmpty(value.StartTime)
            ? string.Empty
            : $"{value.DayName} {value.StartTime}-{value.EndTime}";
        UpdateTimeLabels();
    }

    private static TimeSpan? ParseTime(string? t)
        => ScheduleExpander.TryParseTime(t, out var ts) ? ts : null;

    // Окно видимости для текущего режима: начало и число дней.
    private (DateTime Start, int Days) VisibilityWindow()
    {
        var d = SelectedDate.Date;
        return Mode switch
        {
            ScheduleMode.Day => (d, 1),
            ScheduleMode.Week => (WeekStart(d), 7),
            ScheduleMode.Month => (new DateTime(d.Year, d.Month, 1), DateTime.DaysInMonth(d.Year, d.Month)),
            ScheduleMode.Year => (new DateTime(d.Year, 1, 1), DateTime.IsLeapYear(d.Year) ? 366 : 365),
            _ => (WeekStart(d), 7),
        };
    }

    // Первый день недели-колонки по настройке StartWeekOnMonday.
    private DateTime WeekStart(DateTime d)
        => StartWeekOnMonday
            ? d.AddDays(-((int)d.DayOfWeek + 6) % 7)
            : d.AddDays(-(int)d.DayOfWeek);

    private void RegenerateSchedule()
    {
        // Month сейчас — заглушка, Year — чек-лист: пункты расписания им не нужны.
        if (Mode is ScheduleMode.Month or ScheduleMode.Year)
        {
            Schedule.Clear();
            WeekDayHeaders.Clear();
            WeekRows.Clear();
            return;
        }

        var (windowStart, dayCount) = VisibilityWindow();

        // Разворот всех шаблонов в пункты на даты окна.
        var byDate = new Dictionary<DateTime, List<ScheduleItem>>();
        foreach (var t in TaskTemplates.Concat(OneTimeTasks))
        {
            foreach (var (date, start, end) in ScheduleExpander.Expand(t, windowStart, dayCount))
            {
                var item = new ScheduleItem
                {
                    Date = date,
                    Template = t,
                    StartTime = start,
                    EndTime = end,
                };
                if (!byDate.TryGetValue(date, out var list))
                    byDate[date] = list = new List<ScheduleItem>();
                list.Add(item);
            }
        }

        // Day view: пункты выбранного дня по времени.
        Schedule.Clear();
        if (byDate.TryGetValue(SelectedDate.Date, out var dayItems))
        {
            foreach (var i in dayItems.OrderBy(i => ParseTime(i.StartTime) ?? TimeSpan.MaxValue))
                Schedule.Add(i);
        }

        if (Mode == ScheduleMode.Week)
            BuildWeekGrid(windowStart, byDate);

        OnPropertyChanged(nameof(CustomConflictText));
    }

    // --- Week grid (horizontal timetable: days on top, time on the left) ---
    private void BuildWeekGrid(DateTime weekStart, Dictionary<DateTime, List<ScheduleItem>> byDate)
    {
        WeekDayHeaders.Clear();
        WeekRows.Clear();

        var dates = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i)).ToArray();
        foreach (var d in dates)
            WeekDayHeaders.Add(new WeekDayHeader
            {
                Name = $"{ShortDayName(d.DayOfWeek)} {d.Day}",
                Day = d.DayOfWeek,
                Date = d,
            });
        _dayCellIndex = dates.Select((d, i) => (d, i)).ToDictionary(x => x.d, x => x.i);

        var rawByDay = new Dictionary<DateTime, List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)>>();
        foreach (var (date, items) in byDate)
        {
            foreach (var item in items)
            {
                if (!ScheduleExpander.TryParseTime(item.StartTime, out var s)
                    || !ScheduleExpander.TryParseTime(item.EndTime, out var e))
                    continue;
                AddInterval(rawByDay, date, s, e, item);
            }
        }

        if (rawByDay.Count == 0)
            return;

        var resolvedByDay = new Dictionary<DateTime, List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)>>();
        foreach (var (date, tasks) in rawByDay)
            // ResolveDayWraps ищет перенос через полночь по порядку начала — задачи
            // должны идти по времени, иначе переносится всё, что идёт не по порядку.
            resolvedByDay[date] = ResolveDayWraps(tasks.OrderBy(x => x.Start).ThenBy(x => x.End).ToList());

        var all = resolvedByDay.Values.SelectMany(x => x).ToList();
        var min = all.Min(x => x.Start);
        var max = all.Max(x => x.End);

        var bounds = all
            .SelectMany(x => new[] { x.Start, x.End })
            .Where(t => t >= min && t <= max)
            .OrderBy(t => t)
            .Distinct()
            .ToList();

        for (int i = 0; i < bounds.Count - 1; i++)
        {
            var slotStart = bounds[i];
            var slotEnd = bounds[i + 1];
            var row = new WeekRow
            {
                TimeLabel = $"{FormatTime(slotStart)}-{FormatTime(slotEnd)}",
                DefaultLabel = $"{FormatTime(slotStart)}-{FormatTime(slotEnd)}",
            };
            foreach (var date in dates)
            {
                var cell = new WeekCell();
                if (resolvedByDay.TryGetValue(date, out var list))
                {
                    foreach (var (s, e, item) in list)
                    {
                        if (s < slotEnd && slotStart < e && !cell.Tasks.Contains(item))
                            cell.Tasks.Add(item);
                    }
                }
                row.Cells.Add(cell);
            }
            WeekRows.Add(row);
        }

        UpdateTimeLabels();
        UpdateColumnWidths();
    }

    // Колонка дня занимает минимум (60), но расширяется под самую длинную
    // таску в ней. Ширина считается через реальное измерение текста.
    private void UpdateColumnWidths()
    {
        for (int i = 0; i < WeekDayHeaders.Count; i++)
        {
            double w = 0;
            foreach (var row in WeekRows)
            {
                foreach (var task in row.Cells[i].Tasks)
                {
                    foreach (var line in task.Activity.Split('\n'))
                        w = Math.Max(w, MeasureText(line));
                }
            }
            var width = Math.Max(60, Math.Ceiling(w) + 16);
            WeekDayHeaders[i].ColumnWidth = width;
            foreach (var row in WeekRows)
                row.Cells[i].ColumnWidth = width;
        }
    }

    private static double MeasureText(string text, double fontSize = 11)
    {
        var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface(FontFamily.Default), fontSize, Brushes.Black);
        return ft.Width;
    }

    // Столбец Time подстраивается под выбранный день: строка показывает
    // времена тасок этого дня, а если в этот день на строке ничего нет — пусто.
    // Без выбранной таски — общие интервалы строки по всем дням.
    private void UpdateTimeLabels()
    {
        var date = SelectedItem?.Date;
        var useDay = date != null
            && _dayCellIndex != null
            && _dayCellIndex.ContainsKey(date.Value)
            && !string.IsNullOrEmpty(SelectedItem!.StartTime);

        foreach (var header in WeekDayHeaders)
            header.IsSelected = useDay && header.Date == date;

        if (_dayCellIndex == null)
            return;

        foreach (var row in WeekRows)
        {
            if (!useDay)
            {
                row.TimeLabel = row.DefaultLabel;
                continue;
            }

            var cell = row.Cells[_dayCellIndex[date!.Value]];
            row.TimeLabel = string.Join("+", cell.Tasks
                .Select(t => $"{t.StartTime}-{t.EndTime}")
                .Distinct());
        }
    }

    // A day's tasks that run past midnight wrap: once a task starts earlier than the
    // previous one, the day has rolled into the next one — shift everything forward 24h.
    private static List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)> ResolveDayWraps(
        List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)> tasks)
    {
        var result = new List<(TimeSpan, TimeSpan, ScheduleItem)>(tasks.Count);
        var offset = TimeSpan.Zero;
        TimeSpan? prevRawStart = null;
        foreach (var (start, end, item) in tasks)
        {
            if (prevRawStart != null && start < prevRawStart.Value)
                offset += TimeSpan.FromHours(24);
            result.Add((start + offset, end + offset, item));
            prevRawStart = start;
        }
        return result;
    }

    private static void AddInterval(
        Dictionary<DateTime, List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)>> map,
        DateTime date, TimeSpan start, TimeSpan end, ScheduleItem item)
    {
        if (end <= start)
            return;
        if (!map.TryGetValue(date, out var list))
            map[date] = list = new List<(TimeSpan, TimeSpan, ScheduleItem)>();
        list.Add((start, end, item));
    }

    private static string FormatTime(TimeSpan t)
        => $"{(int)t.TotalHours % 24:00}:{t.Minutes:00}";

    private static string ShortDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        _ => "Sun",
    };

    // --- Navigation commands ---
    [RelayCommand]
    private void PreviousPeriod() => ShiftPeriod(-1);

    [RelayCommand]
    private void NextPeriod() => ShiftPeriod(1);

    [RelayCommand]
    private void GoToToday() => SelectedDate = DateTime.Today;

    private void ShiftPeriod(int direction)
    {
        SelectedDate = Mode switch
        {
            ScheduleMode.Day => SelectedDate.AddDays(direction),
            ScheduleMode.Week => SelectedDate.AddDays(7 * direction),
            ScheduleMode.Month => SelectedDate.AddMonths(direction),
            ScheduleMode.Year => SelectedDate.AddYears(direction),
            _ => SelectedDate,
        };
    }
}
