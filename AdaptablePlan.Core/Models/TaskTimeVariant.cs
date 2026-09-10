using System;
using System.Collections.Generic;

namespace AdaptablePlan.Core.Models;

// Один вариант времени задачи: дни недели, на которые он действует,
// и интервал "HH:mm". Пустой EndTime = Start + DurationMinutes.
public class TaskTimeVariant
{
    public HashSet<DayOfWeek> Days { get; set; } = new();
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}
