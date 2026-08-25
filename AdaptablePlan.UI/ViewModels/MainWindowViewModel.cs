using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdaptablePlan.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAdaptablePlanDb? _db;
    private readonly DbType _dbType;

    // --- Default (seed) data ---
    private static readonly ScheduleEntry[] DefaultSchedule =
    [
        new() { Id = Guid.NewGuid(), StartTime = "09:00", EndTime = "09:30", Activity = "Task A" },
        new() { Id = Guid.NewGuid(), StartTime = "09:30", EndTime = "10:00", Activity = "Task B" },
        new() { Id = Guid.NewGuid(), StartTime = "10:00", EndTime = "10:15", Activity = "Break" },
        new() { Id = Guid.NewGuid(), StartTime = "10:15", EndTime = "11:00", Activity = "Task C" },
        new() { Id = Guid.NewGuid(), StartTime = "11:00", EndTime = "11:30", Activity = "Task D" },
    ];

    private static TaskTemplate MakeDefaultTask(int pos, string name, TaskType type, string start, string end, DayOfWeek[] days)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            DurationMinutes = type is TaskType.Recurring or TaskType.Things ? 30 : 0,
            Position = pos,
            StartTime = start,
            EndTime = end,
            DaysOfWeek = new HashSet<DayOfWeek>(days),
        };

    private static IEnumerable<TaskTemplate> DefaultTaskTemplates()
    {
        yield return MakeDefaultTask(0, "DEFAULT — DB not loaded", TaskType.Recurring, "09:00", "09:30", [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]);
        yield return MakeDefaultTask(1, "Morning standup", TaskType.FixedTime, "09:00", "09:15", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
        yield return MakeDefaultTask(2, "Deep work block", TaskType.Recurring, "10:00", "12:00", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday]);
        yield return MakeDefaultTask(3, "Lunch break", TaskType.FixedTime, "12:30", "13:00", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
    }

    public ObservableCollection<ScheduleEntry> Schedule { get; } = new();
    public ObservableCollection<TaskTemplate> TaskTemplates { get; } = new();
    public ObservableCollection<TaskTemplate> OneTimeTasks { get; } = new();

    [ObservableProperty]
    private bool _isNewTaskOpen;

    [ObservableProperty]
    private TaskTemplate _currentTask = new();

    [ObservableProperty]
    private bool _isRecurring;

    [ObservableProperty]
    private bool _isFixedTime;

    [ObservableProperty]
    private bool _isOneTime;

    [ObservableProperty]
    private bool _showDaysOfWeek;

    [ObservableProperty]
    private bool _showDuration;

    [ObservableProperty]
    private bool _isRecurringOrThings;

    [ObservableProperty]
    private bool _dayStarted;

    [ObservableProperty]
    private DateTime _dayStartTime;

    public Dictionary<DayOfWeek, bool> DaysOfWeekSelection { get; } = new();

    public IEnumerable<TaskType> TaskTypes => Enum.GetValues<TaskType>();

    [ObservableProperty]
    private string _appStatus = "Loading...";

    public string DayStartedText => _dayStarted ? "End Day" : "Start Day";

    // ---------------------------------------------------------------------------
    //  Parameterless constructor — used by ViewModelLocator / design-time
    // ---------------------------------------------------------------------------
    public MainWindowViewModel()
    {
        InitCommon();
        LoadDefaultData("⚠ Default data — database not connected");
    }

    // ---------------------------------------------------------------------------
    //  Constructor with DB — used by DI
    // ---------------------------------------------------------------------------
    public MainWindowViewModel(IAdaptablePlanDb db, DbType dbType)
    {
        _db = db;
        _dbType = dbType;

        InitCommon();
        _ = LoadFromDbAsync();
    }

    private void InitCommon()
    {
        var days = Enum.GetValues<DayOfWeek>();
        foreach (var day in days)
            DaysOfWeekSelection[day] = false;

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
        IsFixedTime = CurrentTask.Type == TaskType.FixedTime;
        IsOneTime = CurrentTask.Type == TaskType.OneTime;
        ShowDaysOfWeek = CurrentTask.Type != TaskType.OneTime;
        ShowDuration = CurrentTask.Type == TaskType.Recurring || CurrentTask.Type == TaskType.Things;
        IsRecurringOrThings = CurrentTask.Type == TaskType.Recurring || CurrentTask.Type == TaskType.Things;
    }

    // ---------------------------------------------------------------------------
    //  DB loading / seeding
    // ---------------------------------------------------------------------------
    private async Task LoadFromDbAsync()
    {
        try
        {
            var (templates, schedule) = await ReadAllAsync(_db!, _dbType);

            // If DB is empty, seed it with defaults
            if (!templates.Any() && !schedule.Any())
            {
                await SeedDbAsync(_db!, DefaultTaskTemplates(), DefaultSchedule);

                templates = DefaultTaskTemplates().ToList();
                schedule = DefaultSchedule;
            }

            // Load into UI collections
            foreach (var s in schedule)
                Schedule.Add(s);

            foreach (var t in templates)
                TaskTemplates.Add(t);

            AppStatus = templates.Any()
                ? $"✓ Data loaded from {_dbType}"
                : $"✓ {_dbType} ready (seeded with defaults)";
        }
        catch
        {
            // DB unavailable — fall back to hardcoded defaults
            LoadDefaultData("⚠ Default data — database not connected");
        }
    }

    private void LoadDefaultData(string statusText)
    {
        foreach (var s in DefaultSchedule)
            Schedule.Add(s);

        var defaults = DefaultTaskTemplates().ToList();
        foreach (var t in defaults)
            TaskTemplates.Add(t);

        AppStatus = statusText;
    }

    // ---------------------------------------------------------------------------
    //  Static helpers — read / seed
    // ---------------------------------------------------------------------------
    public static async Task<(IReadOnlyList<TaskTemplate> templates, IReadOnlyList<ScheduleEntry> schedule)> ReadAllAsync(IAdaptablePlanDb db, DbType type)
    {
        var templates = await db.TaskTemplates.GetAllAsync();
        var schedule = await db.ScheduleEntries.GetAllAsync();
        return (templates, schedule);
    }

    public static async Task SeedDbAsync(IAdaptablePlanDb db, IEnumerable<TaskTemplate> templates, IEnumerable<ScheduleEntry> schedule)
    {
        foreach (var t in templates)
            await db.TaskTemplates.InsertAsync(t);
        foreach (var s in schedule)
            await db.ScheduleEntries.InsertAsync(s);
    }

    // ---------------------------------------------------------------------------
    //  Commands
    // ---------------------------------------------------------------------------
    [RelayCommand]
    private void OpenNewTask()
    {
        CurrentTask = new();
        CurrentTask.Type = TaskType.Recurring;
        foreach (var day in DaysOfWeekSelection.Keys)
            DaysOfWeekSelection[day] = false;
        IsNewTaskOpen = true;
    }

    [RelayCommand]
    private void OnTaskTypeChanged(TaskType type)
    {
        foreach (var day in DaysOfWeekSelection.Keys)
            DaysOfWeekSelection[day] = false;
    }

    [RelayCommand]
    private async Task SaveNewTask()
    {
        if (string.IsNullOrWhiteSpace(CurrentTask.Name))
            return;

        var task = new TaskTemplate
        {
            Id = CurrentTask.Id != Guid.NewGuid() ? CurrentTask.Id : Guid.NewGuid(),
            Name = CurrentTask.Name,
            Type = CurrentTask.Type,
            DurationMinutes = CurrentTask.DurationMinutes,
            Position = CurrentTask.Position,
            StartTime = CurrentTask.StartTime,
            EndTime = CurrentTask.EndTime,
            Date = CurrentTask.Date,
        };

        if (task.Type != TaskType.OneTime)
        {
            task.DaysOfWeek = new HashSet<DayOfWeek>();
            foreach (var kvp in DaysOfWeekSelection)
                if (kvp.Value)
                    task.DaysOfWeek.Add(kvp.Key);
        }

        if (task.Type == TaskType.OneTime)
            OneTimeTasks.Add(task);
        else
            TaskTemplates.Add(task);

        // Persist to DB
        if (_db != null)
            await _db.TaskTemplates.InsertAsync(task);

        IsNewTaskOpen = false;
    }

    [RelayCommand]
    private void CancelNewTask()
    {
        IsNewTaskOpen = false;
    }

    [RelayCommand]
    private void ToggleDay()
    {
        if (DayStarted)
            DayStarted = false;
        else
        {
            DayStarted = true;
            DayStartTime = DateTime.Now;
        }

        OnPropertyChanged(nameof(DayStartedText));
    }

    [RelayCommand]
    private void FinishCurrentTask()
    {
    }
}
