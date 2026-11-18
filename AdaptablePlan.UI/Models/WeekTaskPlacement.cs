namespace AdaptablePlan.UI.Models;

// Положение задачи в колонке дня недельной сетки для отрисовки со span'ом:
// Row - индекс строки в WeekRows, где задача начинается, Span - число
// занятых строк (задача рисуется одной ячейкой).
public sealed class WeekTaskPlacement
{
    public ScheduleItem Task { get; init; } = null!;

    public int Row { get; set; }

    public int Span { get; set; } = 1;
}
