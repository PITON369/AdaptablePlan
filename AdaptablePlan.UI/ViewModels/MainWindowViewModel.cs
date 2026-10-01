using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;

namespace AdaptablePlan.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAdaptablePlanDb? _db;
    private string _sqlitePath = string.Empty;

    private static TaskTemplate MakeDefaultTask(string name, TaskType type, int durationMinutes, params TaskTimeVariant[] variants)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            DurationMinutes = durationMinutes,
            DayTimes = [.. variants],
        };

    private static TaskTimeVariant Time(string start, string end, params DayOfWeek[] days)
        => new() { StartTime = start, EndTime = end, Days = [.. days] };

    // Демо-набор: все типы задач и разные сочетания дней/времён.
    private static List<TaskTemplate> DefaultTaskTemplates()
    {
        DayOfWeek[] monFri = [DayOfWeek.Monday, DayOfWeek.Friday];
        DayOfWeek[] tueThu = [DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday];
        DayOfWeek[] weekend = [DayOfWeek.Saturday, DayOfWeek.Sunday];
        DayOfWeek[] weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
        DayOfWeek[] allWeek = [.. weekdays, .. weekend];

        var today = DateTime.Today;

        return
        [
            // Одна задача — три разных времени по группам дней.
            MakeDefaultTask("Morning standup", TaskType.FixedTime, 0,
                Time("09:00", "09:10", monFri),
                Time("09:00", "09:20", tueThu)),
            MakeDefaultTask("Study session", TaskType.Recurring, 45,
                Time("09:10", "", monFri),
                Time("09:20", "", tueThu),
                Time("10:00", "", weekend)),
            // Задача на все 7 дней: будни и выходные по-разному.
            MakeDefaultTask("Cook dinner", TaskType.FixedTime, 0,
                Time("19:15", "19:55", monFri),
                Time("19:00", "19:40", tueThu),
                Time("18:00", "18:40", weekend)),
            // На Вт–Чт пересекается со Study session (09:20-10:05) — demo: две таски в одной ячейке.
            MakeDefaultTask("Deep work block", TaskType.Recurring, 90,
                Time("10:30", "", monFri),
                Time("10:00", "", tueThu)),
            MakeDefaultTask("Lunch break", TaskType.FixedTime, 0,
                Time("12:30", "13:00", monFri),
                Time("12:30", "13:00", tueThu),
                Time("13:30", "14:00", weekend)),
            // Одна задача, дни не подряд.
            MakeDefaultTask("Hobby hour", TaskType.Recurring, 60,
                Time("14:00", "", DayOfWeek.Wednesday),
                Time("15:00", "", DayOfWeek.Sunday)),
            MakeDefaultTask("Gym workout", TaskType.Recurring, 40,
                Time("18:30", "", monFri),
                Time("19:45", "", tueThu)),
            MakeDefaultTask("Evening review", TaskType.Things, 30,
                Time("20:30", "", tueThu),
                Time("20:00", "", weekend)),
            // Только выходные.
            MakeDefaultTask("Long walk", TaskType.Recurring, 60,
                Time("11:30", "", weekend)),
            MakeDefaultTask("Weekly planning", TaskType.FixedTime, 0,
                Time("16:00", "16:30", DayOfWeek.Sunday)),
            // Разовые задачи без времени (только день).
            new TaskTemplate { Id = Guid.NewGuid(), Name = "Pay utilities", Type = TaskType.OneTime, Date = today },
            new TaskTemplate { Id = Guid.NewGuid(), Name = "Call grandma", Type = TaskType.OneTime, Date = today.AddDays(1) },
        ];
    }

    public ObservableCollection<ScheduleItem> Schedule { get; } = new();
    public ObservableCollection<TaskTemplate> TaskTemplates { get; } = new();
    public ObservableCollection<TaskTemplate> OneTimeTasks { get; } = new();
    public ObservableCollection<WeekDayHeader> WeekDayHeaders { get; } = new();
    public ObservableCollection<WeekRow> WeekRows { get; } = new();

    [ObservableProperty]
    private ScheduleItem? _selectedItem;

    [ObservableProperty]
    private bool _isNewTaskOpen;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _confirmDeleteDb;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private TaskTemplate? _selectedTask;

    [ObservableProperty]
    private TaskTemplate _currentTask = new();

    [ObservableProperty]
    private bool _isRecurring;

    [ObservableProperty]
    private bool _isOneTime;

    [ObservableProperty]
    private bool _showDuration;

    [ObservableProperty]
    private bool _isRecurringOrThings;

    [ObservableProperty]
    private bool _dayStarted;

    [ObservableProperty]
    private DateTime _dayStartTime;

    [ObservableProperty]
    private bool _isDayView = true;

    [ObservableProperty]
    private bool _isWeekView;

    [ObservableProperty]
    private bool _startWeekOnMonday = true;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    public ObservableCollection<TaskTimeVariantViewModel> TimeVariants { get; } = new();

    [ObservableProperty]
    private bool _showTimeVariants;

    [ObservableProperty]
    private string _selectedTimeText = string.Empty;

    public IEnumerable<TaskType> TaskTypes => Enum.GetValues<TaskType>();

    [ObservableProperty]
    private string _appStatus = "Loading...";

    // Сервер БД недоступен, но данные загружены из локальной SQLite-копии —
    // жёлтый баннер на весь верх окна.
    [ObservableProperty]
    private bool _isServerDown;

    // Данных нет ни на сервере, ни в локальной копии — показаны дефолтные
    // данные, красный баннер.
    [ObservableProperty]
    private bool _isDatabaseError;

    [ObservableProperty]
    private string _dbBannerText = string.Empty;

    public string DayStartedText => DayStarted ? "End Day" : "Start Day";

    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

    public bool IsMainVisible => !IsNewTaskOpen && !IsSettingsOpen;

    // --- Constructors ---
    public MainWindowViewModel()
    {
        InitCommon();
        LoadDefaultData("Default data — no database");
    }

    public MainWindowViewModel(IAdaptablePlanDb db, DbType dbType, SqliteDbSettings sqliteSettings)
    {
        _db = db;
        _sqlitePath = sqliteSettings.DatabasePath;
        InitCommon();
        LoadFromDb(db, dbType);
    }

    private void InitCommon()
    {
        SyncTaskTypeFlags();
        CurrentTask.PropertyChanged += OnCurrentTaskPropertyChanged;
        PropertyChanged += OnViewModelPropertyChanged;
    }

    private ScheduleItem? _lastSelectedItem;

    // Порядок колонок сетки (индекс ячейки для каждого дня) — нужен для
    // обновления столбца Time при выборе таски.
    private Dictionary<DayOfWeek, int>? _dayCellIndex;

    partial void OnSelectedItemChanged(ScheduleItem? value)
    {
        if (!ReferenceEquals(_lastSelectedItem, value))
        {
            if (_lastSelectedItem != null)
                _lastSelectedItem.IsSelected = false;
            _lastSelectedItem = value;
            if (value != null)
                value.IsSelected = true;
        }
        SelectedTask = value?.Template;
        SelectedTimeText = value == null || string.IsNullOrEmpty(value.StartTime)
            ? string.Empty
            : $"{value.DayName} {value.StartTime}-{value.EndTime}";
        UpdateTimeLabels();
    }

    partial void OnIsDayViewChanged(bool value) => RegenerateSchedule();
    partial void OnIsWeekViewChanged(bool value) => RegenerateSchedule();
    partial void OnStartWeekOnMondayChanged(bool value) => RegenerateSchedule();

    partial void OnIsNewTaskOpenChanged(bool value) => OnPropertyChanged(nameof(IsMainVisible));
    partial void OnIsSettingsOpenChanged(bool value) => OnPropertyChanged(nameof(IsMainVisible));

    partial void OnValidationMessageChanged(string value)
        => OnPropertyChanged(nameof(HasValidationMessage));

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CurrentTask))
        {
            CurrentTask.PropertyChanged -= OnCurrentTaskPropertyChanged;
            CurrentTask.PropertyChanged += OnCurrentTaskPropertyChanged;
            SyncTaskTypeFlags();
        }
    }

    private void OnCurrentTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TaskTemplate.Type))
            SyncTaskTypeFlags();
    }

    private void SyncTaskTypeFlags()
    {
        IsRecurring = CurrentTask.Type == TaskType.Recurring;
        IsOneTime = CurrentTask.Type == TaskType.OneTime;
        ShowTimeVariants = CurrentTask.Type != TaskType.OneTime;
        ShowDuration = CurrentTask.Type == TaskType.Recurring || CurrentTask.Type == TaskType.Things;
        IsRecurringOrThings = CurrentTask.Type == TaskType.Recurring || CurrentTask.Type == TaskType.Things;
    }

    // --- DB load (sync) ---
    // Каскад: серверная БД → локальная SQLite-копия → дефолтные данные.
    private void LoadFromDb(IAdaptablePlanDb db, DbType dbType)
    {
        try
        {
            var templates = db.TaskTemplates.GetAllAsync().GetAwaiter().GetResult();

            // first run — seed
            if (!templates.Any())
            {
                var defaultTasks = DefaultTaskTemplates();
                foreach (var t in defaultTasks)
                    db.TaskTemplates.InsertAsync(t).GetAwaiter().GetResult();
                templates = defaultTasks;
            }

            LoadTemplates(templates);
            IsServerDown = false;
            IsDatabaseError = false;
            AppStatus = dbType == DbType.Sqlite
                ? "Data loaded from local database"
                : "Data loaded from server database";
        }
        catch
        {
            try
            {
                var backup = SqliteBackup.LoadFromSqliteAsync(_sqlitePath).GetAwaiter().GetResult();
                if (backup != null)
                {
                    LoadTemplates(backup.Value.Templates);
                    IsServerDown = true;
                    IsDatabaseError = false;
                    var stamp = SqliteBackup.ReadLastBackupDate(_sqlitePath);
                    var from = string.IsNullOrEmpty(stamp) ? "" : $" (backup from {stamp})";
                    DbBannerText =
                        $"Database server unreachable — using local backup copy{from}. Changes are NOT saved!";
                    AppStatus = "Data loaded from local backup (server unavailable)";
                    return;
                }
            }
            catch
            {
                // локальная копия тоже недоступна — падаем в дефолтные данные
            }

            IsDatabaseError = true;
            DbBannerText = "No database available — showing default data. Changes are NOT saved!";
            LoadDefaultData("Default data — no database available");
        }
    }

    private void LoadTemplates(IEnumerable<TaskTemplate> templates)
    {
        TaskTemplates.Clear();
        OneTimeTasks.Clear();
        foreach (var t in templates)
        {
            if (MigrateLegacyTimes(t) && _db != null)
                _db.TaskTemplates.UpdateAsync(t).GetAwaiter().GetResult();
            (t.Type == TaskType.OneTime ? OneTimeTasks : TaskTemplates).Add(t);
        }
        RegenerateSchedule();
    }

    // Разовая миграция задач, сохранённых до появления DayTimes: одно время
    // и общий список дней превращаются в один вариант. Возвращает true, если меняли.
    private static bool MigrateLegacyTimes(TaskTemplate t)
    {
        if (t.DayTimes.Count > 0 || string.IsNullOrEmpty(t.StartTime))
            return false;
        t.DayTimes.Add(new TaskTimeVariant
        {
            Days = new HashSet<DayOfWeek>(t.DaysOfWeek),
            StartTime = t.StartTime,
            EndTime = t.EndTime,
        });
        t.StartTime = string.Empty;
        t.EndTime = string.Empty;
        t.DaysOfWeek = new HashSet<DayOfWeek>();
        return true;
    }

    private void LoadDefaultData(string statusText)
    {
        TaskTemplates.Clear();
        OneTimeTasks.Clear();
        foreach (var t in DefaultTaskTemplates())
            TaskTemplates.Add(t);
        RegenerateSchedule();
        AppStatus = statusText;
    }

    // --- Weekly schedule generation ---
    private static TimeSpan? ParseTime(string? t)
        => TryParseTime(t, out var ts) ? ts : null;

    // Интервал варианта: конец = EndTime либо Start + DurationMinutes.
    private static (TimeSpan? Start, TimeSpan? End) VariantInterval(TaskTemplate t, TaskTimeVariant v)
    {
        if (!TryParseTime(v.StartTime, out var start))
            return (null, null);
        var end = TryParseTime(v.EndTime, out var e) ? (TimeSpan?)e
            : t.DurationMinutes > 0 ? start + TimeSpan.FromMinutes(t.DurationMinutes) : null;
        if (end == null)
            return (start, null);
        var en = end.Value;
        if (en <= start)
            en = en.Add(TimeSpan.FromHours(24));
        return (start, en);
    }

    // Вариант, действующий на заданный день (нет — задача в этот день не выполняется).
    private static TaskTimeVariant? VariantForDay(TaskTemplate t, DayOfWeek day)
        => t.DayTimes.FirstOrDefault(v => v.Days.Contains(day));

    private static (TimeSpan? Start, TimeSpan? End) IntervalForDay(TaskTemplate t, DayOfWeek day)
    {
        var v = VariantForDay(t, day);
        return v == null ? (null, null) : VariantInterval(t, v);
    }

    private static ScheduleItem? CreateItem(TaskTemplate t, DayOfWeek day)
    {
        var (start, end) = IntervalForDay(t, day);
        if (start == null || end == null)
            return null;
        return new ScheduleItem
        {
            Day = day,
            Template = t,
            StartTime = FormatTime(start.Value),
            EndTime = FormatTime(end.Value),
        };
    }

    // --- Validation ---
    private string? ValidateTask(TaskTemplate task)
    {
        if (string.IsNullOrWhiteSpace(task.Name))
            return "Enter a task name.";

        if (task.Type is TaskType.Recurring or TaskType.Things)
        {
            if (!int.TryParse(DurationText.Trim(), out var minutes) || minutes <= 0)
                return "Duration must be a positive whole number of minutes (e.g. 30).";
        }

        if (task.Type != TaskType.OneTime)
        {
            if (TimeVariants.Count == 0)
                return "Add at least one time (days + start).";

            foreach (var v in TimeVariants)
            {
                if (v.StartValue is null)
                    return "Select a start time for every time slot.";
                if (task.Type == TaskType.FixedTime)
                {
                    if (v.EndValue is null)
                        return "Select an end time for every time slot.";
                    if (v.EndValue <= v.StartValue)
                        return "End time must be later than start time.";
                }
                if (!v.HasAnyDay)
                    return "Select at least one day for every time slot.";
            }
        }

        return null;
    }

    private static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Trim().Split(':');
        if (parts.Length != 2)
            return false;
        if (!int.TryParse(parts[0], out var hours) || !int.TryParse(parts[1], out var minutes))
            return false;
        if (hours is < 0 or > 23 || minutes is < 0 or > 59)
            return false;
        time = new TimeSpan(hours, minutes, 0);
        return true;
    }

    private void RegenerateSchedule()
    {
        var today = DateTime.Today;
        var weekStart = today.AddDays(-WeekIndex(today.DayOfWeek));

        var items = new List<ScheduleItem>();
        foreach (var t in TaskTemplates)
        {
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                if (VariantForDay(t, day) != null)
                    items.Add(CreateItem(t, day) ?? new ScheduleItem { Day = day, Template = t });
            }
        }
        foreach (var t in OneTimeTasks)
        {
            if (t.Date is DateTime d && d.Date >= weekStart && d.Date < weekStart.AddDays(7))
                items.Add(new ScheduleItem { Day = d.DayOfWeek, Template = t });
        }

        var ordered = items
            .Where(i => IsWeekView || i.Day == today.DayOfWeek)
            .OrderBy(i => WeekIndex(i.Day))
            .ThenBy(i => ParseTime(i.StartTime) ?? TimeSpan.MaxValue);

        Schedule.Clear();
        foreach (var i in ordered)
            Schedule.Add(i);

        BuildWeekGrid();
    }

    private int WeekIndex(DayOfWeek day)
        => StartWeekOnMonday ? ((int)day + 6) % 7 : (int)day;

    // --- Week grid (horizontal timetable: days on top, time on the left) ---
    private void BuildWeekGrid()
    {
        WeekDayHeaders.Clear();
        WeekRows.Clear();

        var dayOrder = Enumerable.Range(0, 7)
            .Select(i => (DayOfWeek)(StartWeekOnMonday ? (i + 1) % 7 : i))
            .ToArray();
        foreach (var day in dayOrder)
            WeekDayHeaders.Add(new WeekDayHeader { Name = ShortDayName(day), Day = day });
        _dayCellIndex = dayOrder.Select((d, i) => (Day: d, Index: i))
            .ToDictionary(x => x.Day, x => x.Index);

        var today = DateTime.Today;
        var weekStart = today.AddDays(-WeekIndex(today.DayOfWeek));
        var weekEnd = weekStart.AddDays(7);

        var rawByDay = new Dictionary<DayOfWeek, List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)>>();
        foreach (var t in TaskTemplates)
        {
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                var (start, end) = IntervalForDay(t, day);
                if (start == null || end == null)
                    continue;
                AddInterval(rawByDay, day, start.Value, end.Value, CreateItem(t, day)!);
            }
        }
        foreach (var t in OneTimeTasks)
        {
            if (t.Date is DateTime d && d.Date >= weekStart && d.Date < weekEnd)
            {
                var (start, end) = IntervalForDay(t, d.DayOfWeek);
                if (start == null || end == null)
                    continue;
                AddInterval(rawByDay, d.DayOfWeek, start.Value, end.Value, CreateItem(t, d.DayOfWeek)!);
            }
        }

        if (rawByDay.Count == 0)
            return;

        var resolvedByDay = new Dictionary<DayOfWeek, List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)>>();
        foreach (var (day, tasks) in rawByDay)
            // ResolveDayWraps ищет перенос через полночь по порядку начала — задачи
            // должны идти по времени, иначе переносится всё, что идёт не по порядку.
            resolvedByDay[day] = ResolveDayWraps(tasks.OrderBy(x => x.Start).ThenBy(x => x.End).ToList());

        var all = resolvedByDay.Values.SelectMany(x => x).ToList();
        var min = all.Min(x => x.Start);
        var max = all.Max(x => x.End);

        var bounds = all
            .SelectMany(x => new[] { x.Start, x.End })
            .Where(t => t >= min && t <= max)
            .OrderBy(t => t)
            .Distinct()
            .ToList();

        for (int i = 0; i < bounds.Count - 1; i++)
        {
            var slotStart = bounds[i];
            var slotEnd = bounds[i + 1];
            var row = new WeekRow
            {
                TimeLabel = $"{FormatTime(slotStart)}-{FormatTime(slotEnd)}",
                DefaultLabel = $"{FormatTime(slotStart)}-{FormatTime(slotEnd)}",
            };
            foreach (var day in dayOrder)
            {
                var cell = new WeekCell();
                if (resolvedByDay.TryGetValue(day, out var list))
                {
                    foreach (var (s, e, item) in list)
                    {
                        if (s < slotEnd && slotStart < e && !cell.Tasks.Contains(item))
                            cell.Tasks.Add(item);
                    }
                }
                row.Cells.Add(cell);
            }
            WeekRows.Add(row);
        }

        UpdateTimeLabels();
        UpdateColumnWidths();
    }

    // Колонка дня занимает минимум (60), но расширяется под самую длинную
    // таску в ней. Ширина считается через реальное измерение текста.
    private void UpdateColumnWidths()
    {
        for (int i = 0; i < WeekDayHeaders.Count; i++)
        {
            double w = 0;
            foreach (var row in WeekRows)
            {
                foreach (var task in row.Cells[i].Tasks)
                {
                    foreach (var line in task.Activity.Split('\n'))
                        w = Math.Max(w, MeasureText(line));
                }
            }
            var width = Math.Max(60, Math.Ceiling(w) + 16);
            WeekDayHeaders[i].ColumnWidth = width;
            foreach (var row in WeekRows)
                row.Cells[i].ColumnWidth = width;
        }
    }

    private static double MeasureText(string text, double fontSize = 11)
    {
        var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface(FontFamily.Default), fontSize, Brushes.Black);
        return ft.Width;
    }

    // Столбец Time подстраивается под выбранный день: строка показывает
    // времена тасок этого дня, а если в этот день на строке ничего нет — пусто.
    // Без выбранной таски — общие интервалы строки по всем дням.
    private void UpdateTimeLabels()
    {
        var day = SelectedItem?.Day;
        var useDay = day != null
            && _dayCellIndex != null
            && _dayCellIndex.ContainsKey(day.Value)
            && !string.IsNullOrEmpty(SelectedItem!.StartTime);

        foreach (var header in WeekDayHeaders)
            header.IsSelected = useDay && header.Day == day;

        foreach (var row in WeekRows)
        {
            if (!useDay)
            {
                row.TimeLabel = row.DefaultLabel;
                continue;
            }

            var cell = row.Cells[_dayCellIndex![day!.Value]];
            row.TimeLabel = string.Join("+", cell.Tasks
                .Select(t => $"{t.StartTime}-{t.EndTime}")
                .Distinct());
        }
    }

    // A day's tasks that run past midnight wrap: once a task starts earlier than the
    // previous one, the day has rolled into the next one — shift everything forward 24h.
    private static List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)> ResolveDayWraps(
        List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)> tasks)
    {
        var result = new List<(TimeSpan, TimeSpan, ScheduleItem)>(tasks.Count);
        var offset = TimeSpan.Zero;
        TimeSpan? prevRawStart = null;
        foreach (var (start, end, item) in tasks)
        {
            if (prevRawStart != null && start < prevRawStart.Value)
                offset += TimeSpan.FromHours(24);
            result.Add((start + offset, end + offset, item));
            prevRawStart = start;
        }
        return result;
    }

    private static void AddInterval(
        Dictionary<DayOfWeek, List<(TimeSpan Start, TimeSpan End, ScheduleItem Item)>> map,
        DayOfWeek day, TimeSpan start, TimeSpan end, ScheduleItem item)
    {
        if (end <= start)
            return;
        if (!map.TryGetValue(day, out var list))
            map[day] = list = new List<(TimeSpan, TimeSpan, ScheduleItem)>();
        list.Add((start, end, item));
    }

    private static string FormatTime(TimeSpan t)
        => $"{(int)t.TotalHours % 24:00}:{t.Minutes:00}";

    private static string ShortDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        _ => "Sun",
    };

    // --- Commands ---
    [RelayCommand]
    private void AddTimeVariant() => TimeVariants.Add(new TaskTimeVariantViewModel());

    [RelayCommand]
    private void RemoveTimeVariant(TaskTimeVariantViewModel variant) => TimeVariants.Remove(variant);

    [RelayCommand]
    private void OpenNewTask()
    {
        CurrentTask = new();
        CurrentTask.Type = TaskType.Recurring;
        ValidationMessage = string.Empty;
        DurationText = string.Empty;
        TimeVariants.Clear();
        TimeVariants.Add(new TaskTimeVariantViewModel());
        IsEditing = false;
        IsNewTaskOpen = true;
    }

    [RelayCommand]
    private void SaveNewTask()
    {
        var error = ValidateTask(CurrentTask);
        if (error != null)
        {
            ValidationMessage = error;
            return;
        }

        CurrentTask.Name = CurrentTask.Name.Trim();
        CurrentTask.DurationMinutes = CurrentTask.Type is TaskType.Recurring or TaskType.Things
            ? int.Parse(DurationText.Trim())
            : 0;
        CurrentTask.DayTimes = CurrentTask.Type == TaskType.OneTime
            ? new List<TaskTimeVariant>()
            : TimeVariants.Select(v => v.ToModel()).ToList();

        var conflict = FindConflict(CurrentTask, IsEditing ? SelectedTask : null);
        if (conflict != null)
        {
            ValidationMessage = conflict;
            return;
        }

        ValidationMessage = string.Empty;

        if (IsEditing && SelectedTask != null)
            UpdateTask(SelectedTask);
        else
            InsertTask();

        RegenerateSchedule();
        IsNewTaskOpen = false;
    }

    [RelayCommand]
    private void EditTask()
    {
        if (SelectedTask == null)
            return;

        CurrentTask = new()
        {
            Name = SelectedTask.Name,
            Type = SelectedTask.Type,
            DurationMinutes = SelectedTask.DurationMinutes,
            Date = SelectedTask.Date,
        };
        DurationText = SelectedTask.DurationMinutes.ToString();
        TimeVariants.Clear();
        foreach (var v in SelectedTask.DayTimes)
            TimeVariants.Add(TaskTimeVariantViewModel.FromModel(v));

        ValidationMessage = string.Empty;
        IsEditing = true;
        IsNewTaskOpen = true;
    }

    [RelayCommand]
    private void DeleteTask()
    {
        if (SelectedTask == null || _db == null)
            return;

        if (SelectedTask.Type == TaskType.OneTime)
            OneTimeTasks.Remove(SelectedTask);
        else
            TaskTemplates.Remove(SelectedTask);

        _db.TaskTemplates.DeleteAsync(SelectedTask.Id).GetAwaiter().GetResult();
        SelectedTask = null;
        SelectedItem = null;
        RegenerateSchedule();
    }

    private void UpdateTask(TaskTemplate task)
    {
        task.Name = CurrentTask.Name;
        task.Type = CurrentTask.Type;
        task.DurationMinutes = CurrentTask.DurationMinutes;
        task.Date = CurrentTask.Date;
        task.DayTimes = CurrentTask.DayTimes.ToList();

        if (_db != null)
            _db.TaskTemplates.UpdateAsync(task).GetAwaiter().GetResult();
    }

    private void InsertTask()
    {
        var task = new TaskTemplate
        {
            Id = Guid.NewGuid(),
            Name = CurrentTask.Name,
            Type = CurrentTask.Type,
            DurationMinutes = CurrentTask.DurationMinutes,
            Date = CurrentTask.Date,
            DayTimes = CurrentTask.DayTimes.ToList(),
        };

        if (task.Type == TaskType.OneTime)
            OneTimeTasks.Add(task);
        else
            TaskTemplates.Add(task);

        if (_db != null)
            _db.TaskTemplates.InsertAsync(task).GetAwaiter().GetResult();
    }

    private string? FindConflict(TaskTemplate candidate, TaskTemplate? exclude)
    {
        foreach (var t in TaskTemplates.Concat(OneTimeTasks))
        {
            if (ReferenceEquals(t, exclude))
                continue;
            // У задач OneTime времени нет — конфликт по времени не проверить.
            if (candidate.Type == TaskType.OneTime || t.Type == TaskType.OneTime)
                continue;

            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                var (cs, ce) = IntervalForDay(candidate, day);
                var (ts, te) = IntervalForDay(t, day);
                if (cs == null || ce == null || ts == null || te == null)
                    continue;

                if (cs.Value < te.Value && ts.Value < ce.Value)
                    return $"Time overlaps with '{t.Name}' on {ShortDayName(day)} {FormatTime(ts.Value)}";
            }
        }
        return null;
    }

    [RelayCommand]
    private void CancelNewTask()
    {
        IsNewTaskOpen = false;
    }

    [RelayCommand]
    private void Refresh()
    {
        if (_db == null)
            return;

        LoadTemplates(_db.TaskTemplates.GetAllAsync().GetAwaiter().GetResult());
        AppStatus = "Refreshed from database";
    }

    [RelayCommand]
    private void ToggleDay()
    {
        DayStarted = !DayStarted;
        if (DayStarted)
            DayStartTime = DateTime.Now;
        OnPropertyChanged(nameof(DayStartedText));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        ConfirmDeleteDb = false;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        ConfirmDeleteDb = false;
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void RequestDeleteDatabase() => ConfirmDeleteDb = true;

    [RelayCommand]
    private void CancelDeleteDatabase() => ConfirmDeleteDb = false;

    [RelayCommand]
    private async Task DeleteDatabase()
    {
        if (_db != null)
        {
            await _db.ClearAsync();
            foreach (var t in DefaultTaskTemplates())
                await _db.TaskTemplates.InsertAsync(t);
        }
        LoadDefaultData("Database cleared — defaults restored");
        ConfirmDeleteDb = false;
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void FinishCurrentTask()
    {
    }
}
