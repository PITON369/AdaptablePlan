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

namespace AdaptablePlan.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAdaptablePlanDb? _db;

    private static TaskTemplate MakeDefaultTask(string name, TaskType type, string start, string end, DayOfWeek[] days)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            DurationMinutes = type is TaskType.Recurring or TaskType.Things ? 30 : 0,
            StartTime = start,
            EndTime = end,
            DaysOfWeek = new HashSet<DayOfWeek>(days),
        };

    private static List<TaskTemplate> DefaultTaskTemplates()
    {
        return
        [
            MakeDefaultTask("Morning standup", TaskType.FixedTime, "09:00", "09:15", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]),
            MakeDefaultTask("Deep work block", TaskType.Recurring, "10:00", "12:00", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday]),
            MakeDefaultTask("Lunch break", TaskType.FixedTime, "12:30", "13:00", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]),
        ];
    }

    public ObservableCollection<ScheduleItem> Schedule { get; } = new();
    public ObservableCollection<TaskTemplate> TaskTemplates { get; } = new();
    public ObservableCollection<TaskTemplate> OneTimeTasks { get; } = new();

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
    private bool _isFixedTime;

    [ObservableProperty]
    private bool _isOneTime;

    [ObservableProperty]
    private bool _showDaysOfWeek;

    [ObservableProperty]
    private bool _showDuration;

    [ObservableProperty]
    private bool _showStartTime;

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

    public ObservableCollection<DaySelection> DaysOfWeekSelection { get; } = new();

    [ObservableProperty]
    private bool _allDaysSelected;

    private bool _suppressAllDaysSync;

    public IEnumerable<TaskType> TaskTypes => Enum.GetValues<TaskType>();

    [ObservableProperty]
    private string _appStatus = "Loading...";

    public string DayStartedText => DayStarted ? "End Day" : "Start Day";

    public bool HasValidationMessage => !string.IsNullOrEmpty(ValidationMessage);

    public bool IsMainVisible => !IsNewTaskOpen && !IsSettingsOpen;

    // --- Constructors ---
    public MainWindowViewModel()
    {
        InitCommon();
        LoadDefaultData("Default data — no database");
    }

    public MainWindowViewModel(IAdaptablePlanDb db, DbType dbType)
    {
        _db = db;
        InitCommon();
        LoadFromDb(db, dbType);
    }

    private void InitCommon()
    {
        foreach (var day in Enum.GetValues<DayOfWeek>())
            DaysOfWeekSelection.Add(new DaySelection { Day = day });
        SyncTaskTypeFlags();
        CurrentTask.PropertyChanged += OnCurrentTaskPropertyChanged;
        PropertyChanged += OnViewModelPropertyChanged;
    }

    partial void OnSelectedItemChanged(ScheduleItem? value)
        => SelectedTask = value?.Template;

    partial void OnIsDayViewChanged(bool value) => RegenerateSchedule();
    partial void OnIsWeekViewChanged(bool value) => RegenerateSchedule();
    partial void OnStartWeekOnMondayChanged(bool value) => RegenerateSchedule();

    partial void OnIsNewTaskOpenChanged(bool value) => OnPropertyChanged(nameof(IsMainVisible));
    partial void OnIsSettingsOpenChanged(bool value) => OnPropertyChanged(nameof(IsMainVisible));

    partial void OnAllDaysSelectedChanged(bool value)
    {
        if (_suppressAllDaysSync)
            return;
        foreach (var s in DaysOfWeekSelection)
            s.IsSelected = value;
    }

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
        IsFixedTime = CurrentTask.Type == TaskType.FixedTime;
        IsOneTime = CurrentTask.Type == TaskType.OneTime;
        ShowDaysOfWeek = CurrentTask.Type != TaskType.OneTime;
        ShowDuration = CurrentTask.Type == TaskType.Recurring || CurrentTask.Type == TaskType.Things;
        ShowStartTime = CurrentTask.Type != TaskType.OneTime;
        IsRecurringOrThings = CurrentTask.Type == TaskType.Recurring || CurrentTask.Type == TaskType.Things;
    }

    // --- DB load (sync) ---
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
            AppStatus = $"Data loaded from {dbType}";
        }
        catch
        {
            LoadDefaultData("Default data — database error");
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

    // --- Weekly schedule generation ---
    private static TimeSpan? ParseTime(string? t)
        => TimeSpan.TryParse(t, out var ts) ? ts : null;

    private static (TimeSpan? Start, TimeSpan? End) Interval(TaskTemplate t)
    {
        if (!TimeSpan.TryParse(t.StartTime, out var start))
            return (null, null);
        TimeSpan? end = TimeSpan.TryParse(t.EndTime, out var e) ? e : null;
        end ??= t.DurationMinutes > 0 ? start + TimeSpan.FromMinutes(t.DurationMinutes) : null;
        return (start, end);
    }

    private void RegenerateSchedule()
    {
        var today = DateTime.Today;
        var weekStart = today.AddDays(-WeekIndex(today.DayOfWeek));

        var items = new List<ScheduleItem>();
        foreach (var t in TaskTemplates)
        {
            foreach (var day in t.DaysOfWeek)
                items.Add(new ScheduleItem { Day = day, Template = t });
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
    }

    private int WeekIndex(DayOfWeek day)
        => StartWeekOnMonday ? ((int)day + 6) % 7 : (int)day;

    // --- Commands ---
    [RelayCommand]
    private void OpenNewTask()
    {
        CurrentTask = new();
        CurrentTask.Type = TaskType.Recurring;
        foreach (var s in DaysOfWeekSelection)
            s.IsSelected = true;
        AllDaysSelected = true;
        ValidationMessage = string.Empty;
        IsEditing = false;
        IsNewTaskOpen = true;
    }

    [RelayCommand]
    private void SaveNewTask()
    {
        if (string.IsNullOrWhiteSpace(CurrentTask.Name))
        {
            ValidationMessage = "Enter a task name";
            return;
        }

        var conflict = FindConflict(CurrentTask, IsEditing ? SelectedTask : null);
        if (conflict != null)
        {
            ValidationMessage = $"Time overlaps with '{conflict.Name}' at {conflict.StartTime}";
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
            StartTime = SelectedTask.StartTime,
            EndTime = SelectedTask.EndTime,
            Date = SelectedTask.Date,
        };

        foreach (var s in DaysOfWeekSelection)
            s.IsSelected = SelectedTask.DaysOfWeek.Contains(s.Day);
        _suppressAllDaysSync = true;
        AllDaysSelected = DaysOfWeekSelection.All(s => s.IsSelected);
        _suppressAllDaysSync = false;

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
        task.StartTime = CurrentTask.StartTime;
        task.EndTime = CurrentTask.EndTime;
        task.Date = CurrentTask.Date;

        task.DaysOfWeek.Clear();
        if (task.Type != TaskType.OneTime)
        {
            foreach (var s in DaysOfWeekSelection)
                if (s.IsSelected)
                    task.DaysOfWeek.Add(s.Day);
        }
        task.DaysOfWeek = new HashSet<DayOfWeek>(task.DaysOfWeek);

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
            StartTime = CurrentTask.StartTime,
            EndTime = CurrentTask.EndTime,
            Date = CurrentTask.Date,
            DaysOfWeek = new HashSet<DayOfWeek>(),
        };

        if (task.Type != TaskType.OneTime)
        {
            foreach (var s in DaysOfWeekSelection)
                if (s.IsSelected)
                    task.DaysOfWeek.Add(s.Day);
        }

        if (task.Type == TaskType.OneTime)
            OneTimeTasks.Add(task);
        else
            TaskTemplates.Add(task);

        if (_db != null)
            _db.TaskTemplates.InsertAsync(task).GetAwaiter().GetResult();
    }

    private TaskTemplate? FindConflict(TaskTemplate candidate, TaskTemplate? exclude)
    {
        foreach (var t in TaskTemplates.Concat(OneTimeTasks))
        {
            if (ReferenceEquals(t, exclude))
                continue;

            bool sharedDays;
            if (candidate.Type == TaskType.OneTime && t.Type == TaskType.OneTime)
                sharedDays = candidate.Date.HasValue && candidate.Date.Value.Date == t.Date?.Date;
            else if (candidate.Type != TaskType.OneTime && t.Type != TaskType.OneTime)
                sharedDays = candidate.DaysOfWeek.Overlaps(t.DaysOfWeek);
            else
                sharedDays = false;

            if (!sharedDays)
                continue;

            var (cs, ce) = Interval(candidate);
            var (ts, te) = Interval(t);
            if (cs == null || ce == null || ts == null || te == null)
                continue;

            if (cs.Value < te.Value && ts.Value < ce.Value)
                return t;
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
