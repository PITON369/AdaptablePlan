using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdaptablePlan.Core.Models;

public partial class ScheduleEntry : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private string _startTime = string.Empty;

    [ObservableProperty]
    private string _endTime = string.Empty;

    [ObservableProperty]
    private string _activity = string.Empty;
}
