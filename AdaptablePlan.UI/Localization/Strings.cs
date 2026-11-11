using Avalonia;
using System;
using System.Collections.Generic;

namespace AdaptablePlan.UI.Localization;

// Единая таблица строк интерфейса: ключ -> (английский, русский).
// Apply() заливает значения в ресурсы приложения, XAML берёт их через
// {DynamicResource ключ}; при смене языка значения обновляются сами.
// Код читает строки через Get(). Язык по умолчанию выбирается по системе.
public static class Strings
{
    private static readonly Dictionary<string, (string En, string Ru)> Table = new()
    {
        // Кнопки и общие надписи
        ["Add"] = ("Add", "Добавить"),
        ["Close"] = ("Close", "Закрыть"),
        ["Save"] = ("Save", "Сохранить"),
        ["Edit"] = ("Edit", "Редактировать"),
        ["Clear"] = ("Clear", "Очистить"),
        ["Cancel"] = ("Cancel", "Отмена"),
        ["Update"] = ("Update", "Обновить"),
        ["Delete"] = ("Delete", "Удалить"),
        ["Remove"] = ("Remove", "Убрать"),
        ["Refresh"] = ("Refresh", "Обновить"),
        ["NewTask"] = ("New task", "Новая задача"),
        ["Settings"] = ("Settings", "Настройки"),
        ["Today"] = ("Today", "Сегодня"),
        ["StartWeekOnMonday"] = ("Start week on Monday", "Неделя начинается с понедельника"),
        ["OK"] = ("OK", "OK"),
        ["Browse"] = ("Browse...", "Обзор..."),
        ["YesDelete"] = ("Yes, delete", "Да, удалить"),
        ["AddTime"] = ("Add time", "Добавить время"),
        ["AddPlan"] = ("Add plan", "Добавить план"),
        ["AddSphere"] = ("+ Add sphere", "+ Добавить сферу"),
        ["NewItem"] = ("New item", "Новый пункт"),
        ["Start"] = ("Start", "Начало"),
        ["End"] = ("End", "Конец"),
        ["Date"] = ("Date", "Дата"),

        // Колонки дня
        ["Plan"] = ("Plan", "План"),
        ["Shift"] = ("Shift", "Сдвиг"),
        ["Task"] = ("Task", "Задача"),
        ["Time"] = ("Time", "Время"),

        // Режимы и виды страниц
        ["Day"] = ("Day", "День"),
        ["Week"] = ("Week", "Неделя"),
        ["Month"] = ("Month", "Месяц"),
        ["Year"] = ("Year", "Год"),
        ["Custom"] = ("Custom", "Свои"),
        ["List"] = ("List", "Список"),
        ["Note"] = ("Note", "Заметка"),
        ["MonthPlaceholder"] = ("Month view - placeholder", "Вид месяца - заглушка"),

        // Дни недели (коротко)
        ["Mon"] = ("Mon", "Пн"),
        ["Tue"] = ("Tue", "Вт"),
        ["Wed"] = ("Wed", "Ср"),
        ["Thu"] = ("Thu", "Чт"),
        ["Fri"] = ("Fri", "Пт"),
        ["Sat"] = ("Sat", "Сб"),
        ["Sun"] = ("Sun", "Вс"),

        // Редактор задачи
        ["NewTaskHeader"] = ("New Task", "Новая задача"),
        ["EditTaskHeader"] = ("Edit Task", "Изменить задачу"),
        ["Type"] = ("Type", "Тип"),
        ["Name"] = ("Name", "Название"),
        ["DurationMin"] = ("Duration (min)", "Длительность (мин)"),
        ["DurationPlaceholder"] = ("e.g. 30", "напр. 30"),
        ["Times"] = ("Times", "Времена"),
        ["TypeRecurring"] = ("Recurring", "Повторяющаяся"),
        ["TypeFixedTime"] = ("Fixed Time", "Фиксированное время"),
        ["TypeThings"] = ("Things", "Дела"),
        ["TypeOneTime"] = ("One Time (Things)", "Разовая (без времени)"),
        ["DescRecurring"] = ("Repeats on a schedule (days of week, daily, ...). End = start + duration. Time shifts when the day or a task starts late.",
            "Повторяется по расписанию (дни недели, ежедневно, ...). Конец = начало + длительность. Время сдвигается, если день или таска начались позже."),
        ["DescFixedTime"] = ("Fixed start and end times. Never shifts.",
            "Фиксированные начало и конец. Не сдвигается."),
        ["DescThings"] = ("A thing to do on chosen days, with a duration. Shifts like recurring tasks.",
            "Дело на выбранные дни, с длительностью. Сдвигается как повторяющиеся задачи."),
        ["DescOneTime"] = ("One-time task on a specific date, without a time slot.",
            "Разовая задача на конкретную дату, без времени."),
        ["ValName"] = ("Enter a task name.", "Введите название задачи."),
        ["ValDuration"] = ("Duration must be a positive whole number of minutes (e.g. 30).",
            "Длительность - положительное целое число минут (например, 30)."),
        ["ValAddTime"] = ("Add at least one time (days + start).",
            "Добавьте хотя бы одно время (дни + начало)."),
        ["ValStartMissing"] = ("Select a start time for every time slot.",
            "Выберите время начала для каждого набора."),
        ["ValEndMissing"] = ("Select an end time for every time slot.",
            "Выберите время окончания для каждого набора."),
        ["ValEndAfterStart"] = ("End time must be later than start time.",
            "Время окончания должно быть позже начала."),
        ["ValDayMissing"] = ("Select at least one day for every time slot.",
            "Выберите хотя бы один день для каждого набора."),
        ["ValDateMissing"] = ("Select a date for the task.", "Выберите дату задачи."),
        ["OverlapOn"] = ("Time overlaps with '{0}' on {1}", "Время пересекается с '{0}' ({1})"),
        ["ConflictsWith"] = ("Conflicts with schedule: ", "Пересекается с расписанием: "),
        ["FinishTask"] = ("Finish Task", "Завершить задачу"),
        ["StartTask"] = ("Start Task", "Запустить задачу"),
        ["StartFirstTask"] = ("Start First Task", "Запустить первую задачу"),
        ["TimeLeft"] = ("Time left: ", "Осталось: "),
        ["MinShort"] = ("min", "мин"),
        ["TimerFromList"] = ("From list", "Из списка"),
        ["TimerManual"] = ("Manual", "Вручную"),
        ["TimerStart"] = ("Start timer", "Запустить таймер"),
        ["TimerDone"] = ("Timer finished!", "Таймер завершён!"),

        // Настройки
        ["SettingsHeader"] = ("Settings", "Настройки"),
        ["ScheduleSection"] = ("Schedule", "Расписание"),
        ["AutoStartNext"] = ("Auto-start next task after finishing one",
            "Запускать следующую задачу после завершения"),
        ["HideUnshifted"] = ("Hide unshifted time columns in Day view",
            "Скрывать столбцы несдвинутого времени в режиме День"),
        ["DayStartsAt"] = ("Day starts at", "День начинается в"),
        ["TimeupSound"] = ("Time-up sound", "Звук окончания задачи"),
        ["Appearance"] = ("Appearance", "Внешний вид"),
        ["FontSize"] = ("Font size", "Размер шрифта"),
        ["Theme"] = ("Theme", "Тема"),
        ["Language"] = ("Language", "Язык"),
        ["ThemeSystem"] = ("System", "Системная"),
        ["ThemeLight"] = ("Light", "Светлая"),
        ["ThemeDark"] = ("Dark", "Тёмная"),
        ["LangSystem"] = ("System", "Системный"),
        ["LangEnglish"] = ("English", "Английский"),
        ["LangRussian"] = ("Russian", "Русский"),
        ["Database"] = ("Database", "База данных"),
        ["DeleteDatabase"] = ("Delete database", "Удалить базу данных"),
        ["DeletePastOneTime"] = ("Delete past one-time tasks", "Удалить прошедшие разовые таски"),
        ["DeleteOldOneTime"] = ("Delete these one-time tasks?", "Удалить эти разовые таски?"),
        ["NoOneTimeTasks"] = ("No one-time tasks to delete", "Разовые таски отсутствуют"),
        ["DeleteConfirm"] = ("Delete all data? This cannot be undone.",
            "Удалить все данные? Это действие нельзя отменить."),

        // Статусы и баннеры БД
        ["StatusLoading"] = ("Loading...", "Загрузка..."),
        ["StatusLocalDb"] = ("Data loaded from local database", "Данные загружены из локальной базы"),
        ["StatusServerDb"] = ("Data loaded from server database", "Данные загружены с сервера"),
        ["StatusBackup"] = ("Data loaded from local backup (server unavailable)",
            "Данные загружены из локальной копии (сервер недоступен)"),
        ["StatusRefreshed"] = ("Refreshed from database", "Обновлено из базы"),
        ["StatusDefaultNoDb"] = ("Default data - no database", "Данные по умолчанию - нет базы"),
        ["StatusDefaultNoDbAvailable"] = ("Default data - no database available",
            "Данные по умолчанию - база недоступна"),
        ["StatusCleared"] = ("Database cleared - defaults restored",
            "База очищена - восстановлены данные по умолчанию"),
        ["BannerBackup"] = ("Database server unreachable - using local backup copy{0}. Changes are NOT saved!",
            "Сервер базы данных недоступен - работаем по локальной копии{0}. Изменения НЕ сохраняются!"),
        ["BannerNoDb"] = ("No database available - showing default data. Changes are NOT saved!",
            "База данных недоступна - показаны данные по умолчанию. Изменения НЕ сохраняются!"),

        // Экран планов
        ["PlanMonth"] = ("Month plan", "План месяца"),
        ["PlanYear"] = ("Year plan", "План года"),
        ["WeekN"] = ("Week {0}", "Неделя {0}"),
        ["WantToLearn"] = ("Want to learn", "Хочу научиться"),
        ["WantToImprove"] = ("Want to improve", "Хочу улучшить"),
        ["WantToTry"] = ("Want to try", "Хочу попробовать"),
        ["WantToAchieve"] = ("Want to achieve", "Хочу достичь"),
        ["Spheres"] = ("Areas I want to improve", "Сферы, которые хочу прокачать"),
        ["Articles"] = ("Articles and books", "Статьи и литература"),
        ["Courses"] = ("Courses and webinars", "Курсы и вебинары"),
        ["Events"] = ("Events", "Мероприятия"),
        ["PlansForMonth"] = ("Plans for the month", "Планы на месяц"),
        ["PlansForYear"] = ("Plans for the year", "Планы на год"),

        // Оверлей времени
        ["TimeIsUp"] = ("Time is up!", "Время вышло!"),
    };

    public static string Language { get; private set; } = "en";

    public static event Action? Changed;

    public static void SetLanguage(string code)
    {
        Language = code == "ru" ? "ru" : "en";
        Apply();
        Changed?.Invoke();
    }

    public static string Get(string key)
        => Table.TryGetValue(key, out var v) ? (Language == "ru" ? v.Ru : v.En) : key;

    public static string Format(string key, params object[] args)
        => string.Format(Get(key), args);

    public static void Apply()
    {
        var res = Application.Current?.Resources;
        if (res == null)
            return;
        foreach (var (key, value) in Table)
            res[key] = Language == "ru" ? value.Ru : value.En;
    }
}
