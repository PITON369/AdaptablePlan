using AdaptablePlan.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace AdaptablePlan.UI.Models;

public partial class ScheduleItem : ObservableObject
{
    // Конкретная дата пункта расписания (из разворота шаблона).
    public DateTime Date { get; init; }

    public DayOfWeek Day => PlanDay.DayOfWeek;
    public TaskTemplate Template { get; init; } = new();

    // Пустые, если у пункта нет времени (задача без интервала на этот день).
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;

    // Базовое (несдвинутое) время по плану — для столбцов рядом со сдвинутым.
    public string BaseStartTime { get; init; } = string.Empty;
    public string BaseEndTime { get; init; } = string.Empty;

    // План-день: дата с учётом границы дня — базовый старт до DayStart
    // относится к предыдущему дню. Date остаётся календарной датой разворота.
    public DateTime PlanDay { get; init; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isDone;

    // Непустой у обёрток записей Custom-страниц (отчёт кастомной страницы).
    // По нему клики маршрутизируются в данные страницы, а не в расписание.
    public CustomEntry? CustomSource { get; init; }

    // Галочка выполнения активна только для сегодняшних пунктов —
    // DayOffset хранит состояние одного дня. Заполняется при создании
    // (PlanDay сравнивается с "сегодня" по границе дня).
    public bool IsToday { get; init; }

    public string Activity => Template.Name;

    public string DayName => Day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        _ => "Sun",
    };
}
