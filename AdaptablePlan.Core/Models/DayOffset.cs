using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace AdaptablePlan.Core.Models;

// Одно событие сдвига дня: точка в базовом времени дня (в минутах от 00:00,
// может быть > 1440 для задач через полночь) и дельта с этого момента.
// База — плановое время таски, запустившей событие (старт или конец).
public class DayShiftEvent
{
    public int TimeMinutes { get; set; }
    public int DeltaMinutes { get; set; }
}

// Состояние дня: накопительные сдвиги расписания («начать таску»/«закончить
// таску») и какая таска сейчас идёт. Одна запись с фиксированным Id — новый
// день перезатирает старый.
// Дата строкой yyyy-MM-dd, чтобы не зависеть от таймзоны при хранении в Mongo.
// IgnoreExtraElements: старые документы с OffsetMinutes и Started (до
// событийной модели) должны читаться без исключения — Mongo-драйвер иначе
// роняет GetAllAsync.
[BsonIgnoreExtraElements]
public class DayOffset
{
    public string Id { get; set; } = "day-offset";
    public string Date { get; set; } = string.Empty;
    public List<DayShiftEvent> Events { get; set; } = new();
    public Guid RunningTaskId { get; set; }

    // Выполненные за день таски (по Id шаблона): зелёная отметка/галочка.
    public List<Guid> DoneTaskIds { get; set; } = new();

    // Накопительный сдвиг для таски с базовым стартом baseMinutes.
    public int CumulativeShift(int baseMinutes)
    {
        var minutes = 0;
        foreach (var e in Events)
            if (e.TimeMinutes <= baseMinutes)
                minutes += e.DeltaMinutes;
        return minutes;
    }
}
