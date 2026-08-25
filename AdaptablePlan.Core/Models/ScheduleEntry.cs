using System;

namespace AdaptablePlan.Core.Models;

public class ScheduleEntry
{
    public Guid Id { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
}
