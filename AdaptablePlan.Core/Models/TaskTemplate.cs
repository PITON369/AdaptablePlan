using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdaptablePlan.Core.Models;

public enum TaskType
{
    Recurring,
    FixedTime,
    Things,
    OneTime
}

public partial class TaskTemplate : ObservableObject
{
    public Guid Id { get; init; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private TaskType _type;

    [ObservableProperty]
    private int _durationMinutes;

    [ObservableProperty]
    private string _startTime = string.Empty;

    [ObservableProperty]
    private string _endTime = string.Empty;

    [ObservableProperty]
    private DateTime? _date;

    public HashSet<DayOfWeek> DaysOfWeek { get; set; } = new();
}
