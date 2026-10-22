namespace AdaptablePlan.Core.Models;

// Повторение задачи. «По дням недели» и «еженедельно» объединены в Weekly
// (WeekInterval = 1 — каждую неделю, 2 — через неделю и т.д.).
public enum RecurrenceKind
{
    Once,
    Daily,
    Weekly,
    Monthly,
    Yearly,
}
