namespace AdaptablePlan.Core.Models;

// Сдвиг расписания «кнопкой начала дня»: всё, кроме задач с фиксированным
// временем, сдвигается на OffsetMinutes относительно базовых времён.
// Одна запись с фиксированным Id — новый сдвиг перезатирает старый.
// Дата строкой yyyy-MM-dd, чтобы не зависеть от таймзоны при хранении в Mongo.
public class DayOffset
{
    public string Id { get; set; } = "day-offset";
    public string Date { get; set; } = string.Empty;
    public int OffsetMinutes { get; set; }
}
