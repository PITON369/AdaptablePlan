using System.Collections.Generic;

namespace AdaptablePlan.UI.ViewModels;

public sealed class WeekCell
{
    public List<ScheduleItem> Tasks { get; } = new();

    // Ширина колонки дня в пикселях — считает MainWindowViewModel.BuildWeekGrid
    // по самой длинной таске в колонке.
    public double ColumnWidth { get; set; } = 100;
}
