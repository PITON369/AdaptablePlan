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
    private DateTime? _date;

    // Время задачи: несколько вариантов под разные дни недели
    // (например, Пн+Пт 9:00, Вт-Чт 9:20, Сб-Вс 10:00).
    public List<TaskTimeVariant> DayTimes { get; set; } = new();

    // Старые поля прошлой схемы (одно время на все дни). Нужны только чтобы
    // прочитать прежний JSON и перенести данные в DayTimes при миграции.
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public HashSet<DayOfWeek> DaysOfWeek { get; set; } = new();
}
