using System.Collections.Generic;

namespace AdaptablePlan.UI.ViewModels;

public sealed class WeekRow
{
    public string TimeLabel { get; init; } = string.Empty;
    public List<WeekCell> Cells { get; } = new();
}
