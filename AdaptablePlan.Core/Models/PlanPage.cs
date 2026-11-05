using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace AdaptablePlan.Core.Models;

// Общая версия на каждый вид (месяц и год), без привязки к конкретной дате.
// В Mongo enum хранится как int: при перестановке членов старые данные
// меняют смысл — при изменении порядка пересеять коллекцию.
public enum PlanPageKind
{
    Month,
    Year,
}

// Экран "Планы" (цели и задачи): фиксированные секции-чеклисты, сферы
// с вложенными пунктами, справочные списки и сетка 4 недель по дням.
[BsonIgnoreExtraElements]
public class PlanPage
{
    public Guid Id { get; set; }
    public PlanPageKind Kind { get; set; }
    public List<PlanEntry> Learn { get; set; } = new();
    public List<PlanEntry> Improve { get; set; } = new();
    public List<PlanEntry> Try { get; set; } = new();
    public List<PlanEntry> Achieve { get; set; } = new();
    public List<PlanSphere> Spheres { get; set; } = new();
    public List<PlanEntry> Articles { get; set; } = new();
    public List<PlanEntry> Courses { get; set; } = new();
    public List<PlanEntry> Events { get; set; } = new();
    public List<PlanWeek> Weeks { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class PlanEntry
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDone { get; set; }
}

// Сфера, которую хочется прокачать (Профессия, Спорт, Язык, свои),
// со своим списком того, что конкретно прокачивать.
[BsonIgnoreExtraElements]
public class PlanSphere
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<PlanEntry> Items { get; set; } = new();
}

// Неделя сетки (1..4) с ячейками по дням Пн..Вс.
[BsonIgnoreExtraElements]
public class PlanWeek
{
    public int Number { get; set; }
    public List<PlanDayCell> Days { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class PlanDayCell
{
    public int DayIndex { get; set; }
    public List<PlanEntry> Items { get; set; } = new();
}
