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

namespace AdaptablePlan.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAdaptablePlanDb? _db;

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

    private static List<TaskTemplate> DefaultTaskTemplates()
    {
        return
        [
            MakeDefaultTask(0, "Morning standup", TaskType.FixedTime, "09:00", "09:15", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]),
            MakeDefaultTask(1, "Deep work block", TaskType.Recurring, "10:00", "12:00", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday]),
            MakeDefaultTask(2, "Lunch break", TaskType.FixedTime, "12:30", "13:00", [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]),
        ];
    }

    public ObservableCollection<ScheduleEntry> Schedule { get; } = new();
    public ObservableCollection<TaskTemplate> TaskTemplates { get; } = new();
    public ObservableCollection<TaskTemplate> OneTimeTasks { get; } = new();

    [ObservableProperty]
    private ScheduleEntry? _selectedEntry;

    [ObservableProperty]
    private bool _isEditingEntry;

    [ObservableProperty]
    private string _editEntryActivity = string.Empty;

    [ObservableProperty]
    private string _editEntryStartTime = string.Empty;

    [ObservableProperty]
    private string _editEntryEndTime = string.Empty;

    [ObservableProperty]
    private bool _isNewTaskOpen;

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
    private bool _isRecurringOrThings;

    [ObservableProperty]
    private bool _dayStarted;

    [ObservableProperty]
    private DateTime _dayStartTime;

    public Dictionary<DayOfWeek, bool> DaysOfWeekSelection { get; } = new();

    public IEnumerable<TaskType> TaskTypes => Enum.GetValues<TaskType>();

    [ObservableProperty]
    private string _appStatus = "Loading...";

    public string DayStartedText => DayStarted ? "End Day" : "Start Day";

    // --- Constructor no DB ---
    public MainWindowViewModel()
    {
        InitCommon();
        LoadDefaultData("Default data — no database");
    }

    // --- Constructor with DB ---
    public MainWindowViewModel(IAdaptablePlanDb db, DbType dbType)
    {
        _db = db;
        InitCommon();
        LoadFromDb(db, dbType);
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

    // --- DB load (sync) ---
    private void LoadFromDb(IAdaptablePlanDb db, DbType dbType)
    {
        try
        {
            var templates = db.TaskTemplates.GetAllAsync().GetAwaiter().GetResult();
            var schedule = db.ScheduleEntries.GetAllAsync().GetAwaiter().GetResult();

            // first run — seed
            if (!templates.Any() && !schedule.Any())
            {
                var defaultTasks = DefaultTaskTemplates();
                foreach (var t in defaultTasks)
                    db.TaskTemplates.InsertAsync(t).GetAwaiter().GetResult();
                foreach (var s in DefaultSchedule)
                    db.ScheduleEntries.InsertAsync(s).GetAwaiter().GetResult();

                templates = defaultTasks;
                schedule = DefaultSchedule;
            }

            foreach (var s in schedule)
                Schedule.Add(s);
            foreach (var t in templates)
                TaskTemplates.Add(t);

            AppStatus = templates.Any()
                ? $"Data loaded from {dbType}"
                : $"{dbType} ready (seeded)";
        }
        catch
        {
            LoadDefaultData("Default data — database error");
        }
    }

    private void LoadDefaultData(string statusText)
    {
        foreach (var s in DefaultSchedule)
            Schedule.Add(s);
        foreach (var t in DefaultTaskTemplates())
            TaskTemplates.Add(t);
        AppStatus = statusText;
    }

    // --- Commands ---
    [RelayCommand]
    private void OpenNewTask()
    {
        CurrentTask = new();
        CurrentTask.Type = TaskType.Recurring;
        foreach (var day in DaysOfWeekSelection.Keys)
            DaysOfWeekSelection[day] = false;
        IsEditing = false;
        IsNewTaskOpen = true;
    }

    [RelayCommand]
    private void SaveNewTask()
    {
        if (string.IsNullOrWhiteSpace(CurrentTask.Name))
            return;

        if (IsEditing && SelectedTask != null)
        {
            UpdateTask(SelectedTask);
        }
        else
        {
            InsertTask();
        }

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
            Position = SelectedTask.Position,
            StartTime = SelectedTask.StartTime,
            EndTime = SelectedTask.EndTime,
            Date = SelectedTask.Date,
        };

        foreach (var day in DaysOfWeekSelection.Keys)
            DaysOfWeekSelection[day] = SelectedTask.DaysOfWeek.Contains(day);

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
    }

    private void UpdateTask(TaskTemplate task)
    {
        task.Name = CurrentTask.Name;
        task.Type = CurrentTask.Type;
        task.DurationMinutes = CurrentTask.DurationMinutes;
        task.Position = CurrentTask.Position;
        task.StartTime = CurrentTask.StartTime;
        task.EndTime = CurrentTask.EndTime;
        task.Date = CurrentTask.Date;

        task.DaysOfWeek.Clear();
        if (task.Type != TaskType.OneTime)
        {
            foreach (var kvp in DaysOfWeekSelection)
                if (kvp.Value)
                    task.DaysOfWeek.Add(kvp.Key);
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
            Position = CurrentTask.Position,
            StartTime = CurrentTask.StartTime,
            EndTime = CurrentTask.EndTime,
            Date = CurrentTask.Date,
            DaysOfWeek = new HashSet<DayOfWeek>(),
        };

        if (task.Type != TaskType.OneTime)
        {
            foreach (var kvp in DaysOfWeekSelection)
                if (kvp.Value)
                    task.DaysOfWeek.Add(kvp.Key);
        }

        if (task.Type == TaskType.OneTime)
            OneTimeTasks.Add(task);
        else
            TaskTemplates.Add(task);

        if (_db != null)
            _db.TaskTemplates.InsertAsync(task).GetAwaiter().GetResult();
    }

    // --- Schedule Entry commands ---
    [RelayCommand]
    private void EditEntry()
    {
        if (SelectedEntry == null)
            return;
        EditEntryActivity = SelectedEntry.Activity;
        EditEntryStartTime = SelectedEntry.StartTime;
        EditEntryEndTime = SelectedEntry.EndTime;
        IsEditingEntry = true;
    }

    [RelayCommand]
    private void SaveEntry()
    {
        if (SelectedEntry == null || string.IsNullOrWhiteSpace(EditEntryActivity))
            return;
        SelectedEntry.Activity = EditEntryActivity;
        SelectedEntry.StartTime = EditEntryStartTime;
        SelectedEntry.EndTime = EditEntryEndTime;
        if (_db != null)
            _db.ScheduleEntries.UpdateAsync(SelectedEntry).GetAwaiter().GetResult();
        IsEditingEntry = false;
    }

    [RelayCommand]
    private void CancelEditEntry()
    {
        IsEditingEntry = false;
    }

    [RelayCommand]
    private void DeleteEntry()
    {
        if (SelectedEntry == null || _db == null)
            return;
        Schedule.Remove(SelectedEntry);
        _db.ScheduleEntries.DeleteAsync(SelectedEntry.Id).GetAwaiter().GetResult();
        SelectedEntry = null;
    }

    [RelayCommand]
    private void CancelNewTask()
    {
        IsNewTaskOpen = false;
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
    private void FinishCurrentTask()
    {
    }
}
