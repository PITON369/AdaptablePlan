using System;
using System.Collections.Generic;

namespace AdaptablePlan.Core.Models;

// Вид фиксирован за страницей, данные подстраиваются под вид.
// В Mongo enum хранится как int: при перестановке членов старые данные
// меняют смысл — данные тестовые, при изменении порядка пересеять БД.
public enum PageViewType
{
    Day,
    Week,
    Month,
    Year,
    List,
    Note,
}

// Страница-таблица с планами (по аналогии с Excel-листом).
// Slot 1..10 — фиксированные слоты Custom; Slot 0 — служебная страница Year.
public class CustomPage
{
    public Guid Id { get; set; }
    public int Slot { get; set; }
    public string Name { get; set; } = string.Empty;
    public PageViewType ViewType { get; set; }
    public List<CustomEntry> Entries { get; set; } = new();
    public string NoteText { get; set; } = string.Empty;
}

// Единая форма записи для видов Day/Week/List.
public class CustomEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DayOfWeek Day { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool IsDone { get; set; }
}
