using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.Core.Settings;
using AdaptablePlan.UI.Localization;
using AdaptablePlan.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace AdaptablePlan.UI.ViewModels;

// Состояние и загрузка данных. Расписание — в MainWindowViewModel.Schedule,
// редактор задач — в MainWindowViewModel.TaskEditor, страницы Custom —
// в MainWindowViewModel.CustomPages (partial-классы).
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAdaptablePlanDb? _db;
    private string _sqlitePath = string.Empty;
    private DayOffset? _dayOffset;
    private Guid _runningTaskId;

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
    private bool _startWeekOnMonday = true;

    [ObservableProperty]
    private bool _hideUnshiftedTime;

    // Граница дня: план-время до неё относится к предыдущему дню
    // (00:01-06:59 при старте дня 07:00 - ещё вчера).
    [ObservableProperty]
    private TimeSpan? _dayStart = new(7, 0, 0);

    // Свой звук окончания таски (.wav); пусто - стандартный системный.
    [ObservableProperty]
    private string _soundFile = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationMessage))]
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
    private string _appStatus = Strings.Get("StatusLoading");

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

    // Внешний вид: шрифт и тема, настройки в %APPDATA%\AdaptablePlan\settings.json.
    [ObservableProperty]
    private double _fontSize;

    [ObservableProperty]
    private string _theme = "System";

    [ObservableProperty]
    private string _language = "System";

    partial void OnFontSizeChanged(double value) => SaveAppearance();
    partial void OnHideUnshiftedTimeChanged(bool value) => SaveAppearance();
    partial void OnDayStartChanged(TimeSpan? value)
    {
        RegenerateSchedule();
        SaveAppearance();
    }
    partial void OnSoundFileChanged(string value) => SaveAppearance();

    // "Сегодня" по границе дня: до DayStart утра это ещё предыдущий
    // календарный день - его запись DayOffset и его галочки продолжают жить.
    public DateTime PlanToday
    {
        get
        {
            var start = DayStart ?? new TimeSpan(7, 0, 0);
            var now = DateTime.Now;
            return now.TimeOfDay < start ? now.Date.AddDays(-1) : now.Date;
        }
    }
    partial void OnThemeChanged(string value)
    {
        ApplyTheme();
        SaveAppearance();
    }

    partial void OnLanguageChanged(string value)
    {
        Strings.SetLanguage(new Models.AppSettings { Language = value }.ToLanguageCode());
        SaveAppearance();
    }

    private void ApplyTheme()
    {
        var app = Application.Current;
        if (app == null)
            return;
        app.RequestedThemeVariant = Theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        if (app.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            foreach (var window in desktop.Windows)
                window.RequestedThemeVariant = app.RequestedThemeVariant;
    }

    private void SaveAppearance()
        => new Models.AppSettings
        {
            FontSize = FontSize,
            Theme = Theme,
            Language = Language,
            HideUnshiftedTime = HideUnshiftedTime,
            DayStart = (DayStart ?? new TimeSpan(7, 0, 0)).ToString(@"hh\:mm"),
            SoundFile = SoundFile,
        }.Save();

    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

    // --- Constructors ---
    public MainWindowViewModel()
    {
        InitCommon();
        LoadDefaultData("StatusDefaultNoDb");
        ApplyCustomPages(DefaultCustomPages());
        ApplyPlanPages(DefaultPlanPages());
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
        var settings = Models.AppSettings.Load();
        FontSize = settings.FontSize;
        Theme = settings.Theme;
        Language = settings.Language;
        HideUnshiftedTime = settings.HideUnshiftedTime;
        if (TimeSpan.TryParse(settings.DayStart, out var dayStart))
            DayStart = dayStart;
        SoundFile = settings.SoundFile;
        SelectedDate = PlanToday;
        SyncTaskTypeFlags();
        CurrentTask.PropertyChanged += OnCurrentTaskPropertyChanged;
        PropertyChanged += OnViewModelPropertyChanged;
        Strings.Changed += OnStringsChanged;
        RecomputeStatusTexts();
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
        OnPropertyChanged(nameof(CurrentTaskTypeDescription));
    }

    // Краткая подсказка про выбранный тип в редакторе таски.
    public string CurrentTaskTypeDescription => CurrentTask.Type switch
    {
        TaskType.Recurring => Strings.Get("DescRecurring"),
        TaskType.FixedTime => Strings.Get("DescFixedTime"),
        TaskType.Things => Strings.Get("DescThings"),
        TaskType.OneTime => Strings.Get("DescOneTime"),
        _ => string.Empty,
    };

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
            LoadPlanPages(db);
            _dayOffset = db.DayOffsets.GetAllAsync().GetAwaiter().GetResult().FirstOrDefault();
            RestoreDayState();
            IsServerDown = false;
            IsDatabaseError = false;
            SetStatus(dbType == DbType.Sqlite ? "StatusLocalDb" : "StatusServerDb");
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
                    ApplyPlanPages(DefaultPlanPages());
                    _dayOffset = backup.Value.DayOffsets.FirstOrDefault();
                    RestoreDayState();
                    IsServerDown = true;
                    IsDatabaseError = false;
                    var stamp = SqliteBackup.ReadLastBackupDate(_sqlitePath);
                    var from = string.IsNullOrEmpty(stamp) ? "" : $" (backup from {stamp})";
                    _backupStamp = from;
                    DbBannerText = Strings.Format("BannerBackup", from);
                    SetStatus("StatusBackup");
                    return;
                }
            }
            catch
            {
                // локальная копия тоже недоступна — падаем в дефолтные данные
            }

            IsDatabaseError = true;
            DbBannerText = Strings.Get("BannerNoDb");
            LoadDefaultData("StatusDefaultNoDbAvailable");
            ApplyPlanPages(DefaultPlanPages());
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

    private void LoadDefaultData(string statusKey)
    {
        TaskTemplates.Clear();
        OneTimeTasks.Clear();
        foreach (var t in DefaultTaskTemplates())
            TaskTemplates.Add(t);
        RegenerateSchedule();
        SetStatus(statusKey);
    }

    // --- Commands ---
    [RelayCommand]
    private void Refresh()
    {
        if (_db == null)
            return;

        LoadTemplates(_db.TaskTemplates.GetAllAsync().GetAwaiter().GetResult());
        SetStatus("StatusRefreshed");
    }

    // --- Start/Finish Task ---
    // Одна контекстная кнопка: ничего не идёт — начинает выделенную таску,
    // а при пустом выделении — первую таску дня (бывший «Start Day»: дельта
    // привязывается к её базовому старту); идёт — заканчивает её (дельта =
    // сейчас − отображаемый конец), выделяет следующую таску дня и, при
    // AutoStartNextTask, сразу запускает её.
    [RelayCommand]
    private async Task ToggleTask()
    {
        // В отчёте Custom-страницы механика сдвигов дня (DayOffset) неприменима —
        // кнопка показана для одинакового вида, но ничего не делает.
        if (Mode == ScheduleMode.Custom && !IsCustomEditor)
            return;
        if (_runningTaskId != Guid.Empty)
            await FinishRunningTaskAsync();
        else
        {
            var item = SelectedItem ?? FirstTodayItem();
            if (item != null)
                await StartSelectedTaskAsync(item);
        }
        OnPropertyChanged(nameof(TaskActionText));
        OnPropertyChanged(nameof(CanToggleTask));
    }

    public string TaskActionText
        => IsTaskRunning ? Strings.Get("FinishTask") : SelectedItem != null ? Strings.Get("StartTask") : Strings.Get("StartFirstTask");

    public bool CanToggleTask => IsTaskRunning || SelectedItem != null || FirstTodayItem() != null;

    private bool IsTaskRunning
        => _runningTaskId != Guid.Empty && RunningItem() != null;

    private ScheduleItem? RunningItem()
        => Schedule.FirstOrDefault(i => i.Template.Id == _runningTaskId && i.PlanDay == PlanToday);

    [ObservableProperty]
    private bool _autoStartNextTask;

    // Базовые (без сдвигов) времена таски на её дату — для точек событий.
    private static (TimeSpan? BaseStart, TimeSpan? BaseEnd) BaseInterval(ScheduleItem item)
    {
        var expanded = ScheduleExpander.Expand(item.Template, item.Date, 1);
        if (expanded.Count == 0)
            return (null, null);
        return (ParseTime(expanded[0].Start), ParseTime(expanded[0].End));
    }

    private async Task StartSelectedTaskAsync(ScheduleItem item)
    {
        var (baseStart, _) = BaseInterval(item);
        _runningTaskId = item.Template.Id;
        EnsureDayRecord();

        // Дельта считается арифметически: отображаемое время = база +
        // накопительный сдвиг. Парсить текст нельзя — при уходе за полночь
        // он «завёрнут» (23:xx), и дельта улетает на ±7 часов (лавина сдвигов).
        // FixedTime события не пишет: он всегда идёт по своему времени и
        // ничего о сдвиге расписания не сообщает.
        if (baseStart != null && item.Template.Type != TaskType.FixedTime)
        {
            var baseMin = (int)baseStart.Value.TotalMinutes;
            var delta = (int)DateTime.Now.TimeOfDay.TotalMinutes
                - (baseMin + _dayOffset!.CumulativeShift(baseMin));
            if (delta != 0)
                _dayOffset.Events.Add(new DayShiftEvent { TimeMinutes = baseMin, DeltaMinutes = delta });
        }
        RegenerateSchedule();
        await SaveDayOffsetAsync();
        StartTaskTimer(item);
    }

    private async Task FinishRunningTaskAsync()
    {
        var item = RunningItem();
        if (item == null)
            return;

        StopTaskTimer();

        var (baseStart, baseEnd) = BaseInterval(item);
        var nextIndex = Schedule.IndexOf(item) + 1;
        var next = nextIndex > 0 && nextIndex < Schedule.Count ? Schedule[nextIndex] : null;

        _runningTaskId = Guid.Empty;
        EnsureDayRecord();
        MarkDone(item.Template.Id);
        // Отображаемый конец = база конца + сдвиг, посчитанный по базовому
        // старту (Expand двигает начало и конец одним сдвигом). Арифметика, не
        // парсинг текста — иначе «завёрнутое» за полночь время даёт лавину.
        // FixedTime события не пишет (см. StartSelectedTaskAsync).
        if (baseEnd != null && baseStart != null && item.Template.Type != TaskType.FixedTime)
        {
            var startMin = (int)baseStart.Value.TotalMinutes;
            var delta = (int)DateTime.Now.TimeOfDay.TotalMinutes
                - ((int)baseEnd.Value.TotalMinutes + _dayOffset!.CumulativeShift(startMin));
            if (delta != 0)
                _dayOffset.Events.Add(new DayShiftEvent { TimeMinutes = (int)baseEnd.Value.TotalMinutes, DeltaMinutes = delta });
        }
        RegenerateSchedule();
        await SaveDayOffsetAsync();

        // Ссылки после RegenerateSchedule устаревают — ищем таску заново.
        if (next == null)
            return;
        var nextItem = Schedule.FirstOrDefault(i => i.Template.Id == next.Template.Id && i.Date == next.Date);
        if (nextItem == null)
            return;
        SelectedItem = nextItem;
        if (AutoStartNextTask)
        {
            _runningTaskId = nextItem.Template.Id;
            RegenerateSchedule();
            await SaveDayOffsetAsync();
            StartTaskTimer(nextItem);
        }
    }

    // Отметка выполнения в записи дня (для RegenerateSchedule).
    private void MarkDone(Guid templateId)
    {
        if (_dayOffset != null && !_dayOffset.DoneTaskIds.Contains(templateId))
            _dayOffset.DoneTaskIds.Add(templateId);
    }

    // Ручная галочка в Day-списке; работает только для сегодняшних пунктов —
    // DayOffset хранит состояние одного дня. У обёрток записей Custom-страницы
    // галочка переключает саму запись страницы.
    public async Task ToggleDone(ScheduleItem item)
    {
        if (item.CustomSource != null)
        {
            item.CustomSource.IsDone = !item.CustomSource.IsDone;
            item.IsDone = item.CustomSource.IsDone;
            var page = YearPage != null && YearPage.Model.Entries.Contains(item.CustomSource)
                ? YearPage
                : CustomPages.FirstOrDefault(p => p.Model.Entries.Contains(item.CustomSource));
            if (page != null)
                SaveCustomPage(page);
            return;
        }
        if (!item.IsToday)
            return;
        EnsureDayRecord();
        if (!_dayOffset!.DoneTaskIds.Remove(item.Template.Id))
            _dayOffset.DoneTaskIds.Add(item.Template.Id);
        item.IsDone = _dayOffset.DoneTaskIds.Contains(item.Template.Id);
        await SaveDayOffsetAsync();
    }

    // Запись состояния дня на сегодня; старый день не трогаем — события
    // применяются только к своей дате.
    private void EnsureDayRecord()
    {
        var today = PlanToday.ToString("yyyy-MM-dd");
        if (_dayOffset == null || _dayOffset.Date != today)
            _dayOffset = new DayOffset { Date = today };
    }

    private async Task SaveDayOffsetAsync()
    {
        if (_db == null || _dayOffset == null)
            return;

        try
        {
            var existing = await _db.DayOffsets.GetByIdAsync(_dayOffset.Id);
            if (existing == null)
                await _db.DayOffsets.InsertAsync(_dayOffset);
            else
                await _db.DayOffsets.UpdateAsync(_dayOffset);
        }
        catch
        {
            // сервер недоступен
        }
        try
        {
            await SqliteBackup.SaveDayOffsetAsync(_sqlitePath, _dayOffset);
        }
        catch
        {
            // локальная копия недоступна
        }
    }

    // Первая таска план-сегодняшнего дня: по порядку плана с учётом границы
    // дня (ночные пункты после DayStart-времени следующего календарного дня
    // идут в конец). Когда день показан (SelectedDate = сегодня) — берём
    // реальный пункт из Schedule, иначе строим свежий экземпляр (выделение
    // на него не вешаем: его нет в списке).
    private ScheduleItem? FirstTodayItem()
    {
        var today = PlanToday;
        var dayStart = DayStart ?? new TimeSpan(7, 0, 0);
        if (SelectedDate.Date == today && Mode is not (ScheduleMode.Month or ScheduleMode.Year))
            return Schedule.FirstOrDefault();

        ScheduleItem? first = null;
        var firstStart = TimeSpan.MaxValue;
        foreach (var t in TaskTemplates.Concat(OneTimeTasks))
            foreach (var (date, start, end) in ScheduleExpander.Expand(t, today, 2))
            {
                if (!ScheduleExpander.TryParseTime(start, out var ts))
                    continue;
                var planDay = ts < dayStart ? date.AddDays(-1) : date;
                if (planDay != today)
                    continue;
                var key = ts < dayStart ? ts.Add(TimeSpan.FromHours(24)) : ts;
                if (key >= firstStart)
                    continue;
                firstStart = key;
                first = new ScheduleItem { Date = date, PlanDay = planDay, Template = t, StartTime = start, EndTime = end };
            }
        return first;
    }

    private void RestoreDayState()
    {
        if (_dayOffset != null && _dayOffset.Date == PlanToday.ToString("yyyy-MM-dd"))
            _runningTaskId = _dayOffset.RunningTaskId;
        RegenerateSchedule();
        OnPropertyChanged(nameof(TaskActionText));
        OnPropertyChanged(nameof(CanToggleTask));
    }

    [RelayCommand]
    private void OpenSettings()
    {
        ConfirmDeleteDb = false;
        IsPlanOpen = false;
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
            _dayOffset = null;
            _runningTaskId = Guid.Empty;
            var templates = DefaultTaskTemplates();
            foreach (var t in templates)
                await _db.TaskTemplates.InsertAsync(t);
            var pages = DefaultCustomPages();
            foreach (var p in pages)
                await _db.CustomPages.InsertAsync(p);
            var planPages = DefaultPlanPages();
            foreach (var p in planPages)
                await _db.PlanPages.InsertAsync(p);
            LoadTemplates(templates);
            ApplyCustomPages(pages);
            ApplyPlanPages(planPages);
        }
        else
        {
            LoadDefaultData("StatusCleared");
            ApplyCustomPages(DefaultCustomPages());
            ApplyPlanPages(DefaultPlanPages());
        }
        ConfirmDeleteDb = false;
        IsSettingsOpen = false;
    }
}
