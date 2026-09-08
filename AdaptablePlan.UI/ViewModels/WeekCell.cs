using System.Collections.Generic;

namespace AdaptablePlan.UI.ViewModels;

public sealed class WeekCell
{
    public List<ScheduleItem> Tasks { get; } = new();
}
