using AdaptablePlan.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace AdaptablePlan.UI.ViewModels;

public partial class ScheduleItem : ObservableObject
{
    public DayOfWeek Day { get; init; }
    public TaskTemplate Template { get; init; } = new();

    [ObservableProperty]
    private bool _isSelected;

    public string StartTime => Template.StartTime;
    public string EndTime => Template.EndTime;
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
