using AdaptablePlan.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace AdaptablePlan.UI.Models;

public partial class ScheduleItem : ObservableObject
{
    // Конкретная дата пункта расписания (из разворота шаблона).
    public DateTime Date { get; init; }

    public DayOfWeek Day => Date.DayOfWeek;
    public TaskTemplate Template { get; init; } = new();

    // Пустые, если у пункта нет времени (задача без интервала на этот день).
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

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
