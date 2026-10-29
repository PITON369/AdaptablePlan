using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.Core.Settings;
using AdaptablePlan.UI.Models;
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

// Состояние и загрузка данных. Расписание — в MainWindowViewModel.Schedule,
// редактор задач — в MainWindowViewModel.TaskEditor, страницы Custom —
// в MainWindowViewModel.CustomPages (partial-классы).
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAdaptablePlanDb? _db;
    private string _sqlitePath = string.Empty;
    private DayOffset? _dayOffset;

    private static TaskTemplate MakeDefaultTask(string name, TaskType type, int durationMinutes, params TaskTimeVariant[] variants)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            DurationMinutes = durationMinutes,
            Kind = RecurrenceKind.Weekly,
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

        var today = DateTime.Today;

        return
        [
            // Одна задача — три разных времени по группам дней.
            MakeDefaultTask("Morning standup", TaskType.Recurring, 10,
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
            new TaskTemplate { Id = Guid.NewGuid(), Name = "Pay utilities", Type = TaskType.OneTime, Kind = RecurrenceKind.Once, Date = today },
            new TaskTemplate { Id = Guid.NewGuid(), Name = "Call grandma", Type = TaskType.OneTime, Kind = RecurrenceKind.Once, Date = today.AddDays(1) },
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

    // --- Constructors ---
    public MainWindowViewModel()
    {
        InitCommon();
        LoadDefaultData("Default data — no database");
        ApplyCustomPages(DefaultCustomPages());
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
            LoadCustomPages(db);
            _dayOffset = db.DayOffsets.GetAllAsync().GetAwaiter().GetResult().FirstOrDefault();
            RestoreDayState();
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
                    ApplyCustomPages(backup.Value.Pages.Count > 0 ? backup.Value.Pages : DefaultCustomPages());
                    _dayOffset = backup.Value.DayOffsets.FirstOrDefault();
                    RestoreDayState();
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
            (t.Type == TaskType.OneTime ? OneTimeTasks : TaskTemplates).Add(t);
        RegenerateSchedule();
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

    // --- Commands ---
    [RelayCommand]
    private void Refresh()
    {
        if (_db == null)
            return;

        LoadTemplates(_db.TaskTemplates.GetAllAsync().GetAwaiter().GetResult());
        AppStatus = "Refreshed from database";
    }

    [RelayCommand]
    private async Task ToggleDay()
    {
        DayStarted = !DayStarted;
        if (DayStarted)
        {
            DayStartTime = DateTime.Now;
            await ApplyDayStartOffsetAsync();
        }
        OnPropertyChanged(nameof(DayStartedText));
    }

    // Кнопка «Start Day»: дельта = сейчас − время первой задачи дня (по базовым
    // временам). Все задачи, кроме FixedTime, показываются с этим сдвигом до
    // следующего нажатия (новый сдвиг перезатирает старый).
    private async Task ApplyDayStartOffsetAsync()
    {
        var today = DateTime.Today;
        var first = FirstTaskStart(today);
        if (first == null)
            return;

        var minutes = (int)(DateTime.Now.TimeOfDay - first.Value).TotalMinutes;
        var offset = new DayOffset { Date = today.ToString("yyyy-MM-dd"), OffsetMinutes = minutes };
        _dayOffset = offset;
        RegenerateSchedule();

        if (_db == null)
            return;

        // Сервер, при успехе — сразу локальная копия; сервер недоступен — только локально.
        try
        {
            var existing = await _db.DayOffsets.GetByIdAsync(offset.Id);
            if (existing == null)
                await _db.DayOffsets.InsertAsync(offset);
            else
                await _db.DayOffsets.UpdateAsync(offset);
            try
            {
                await SqliteBackup.SaveDayOffsetAsync(_sqlitePath, offset);
            }
            catch
            {
                // локальная копия недоступна — не критично
            }
        }
        catch
        {
            try
            {
                await SqliteBackup.SaveDayOffsetAsync(_sqlitePath, offset);
            }
            catch
            {
                // нет ни сервера, ни локальной копии — сдвиг только в памяти
            }
        }
    }

    private TimeSpan? FirstTaskStart(DateTime date)
    {
        TimeSpan? first = null;
        foreach (var t in TaskTemplates.Concat(OneTimeTasks))
            foreach (var (_, start, _) in ScheduleExpander.Expand(t, date, 1))
                if (ScheduleExpander.TryParseTime(start, out var ts) && (first == null || ts < first))
                    first = ts;
        return first;
    }

    private void RestoreDayState()
    {
        if (_dayOffset != null && _dayOffset.Date == DateTime.Today.ToString("yyyy-MM-dd"))
        {
            DayStarted = true;
            DayStartTime = DateTime.Now;
        }
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
            var templates = DefaultTaskTemplates();
            foreach (var t in templates)
                await _db.TaskTemplates.InsertAsync(t);
            var pages = DefaultCustomPages();
            foreach (var p in pages)
                await _db.CustomPages.InsertAsync(p);
            LoadTemplates(templates);
            ApplyCustomPages(pages);
        }
        else
        {
            LoadDefaultData("Database cleared — defaults restored");
            ApplyCustomPages(DefaultCustomPages());
        }
        ConfirmDeleteDb = false;
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private void FinishCurrentTask()
    {
    }
}
