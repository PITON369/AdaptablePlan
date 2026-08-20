using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using AdaptablePlan.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AdaptablePlan.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<ScheduleEntry> Schedule { get; } =
    [
        new() { StartTime = "09:00", EndTime = "09:30", Activity = "Task A" },
        new() { StartTime = "09:30", EndTime = "10:00", Activity = "Task B" },
        new() { StartTime = "10:00", EndTime = "10:15", Activity = "Break" },
        new() { StartTime = "10:15", EndTime = "11:00", Activity = "Task C" },
        new() { StartTime = "11:00", EndTime = "11:30", Activity = "Task D" },
    ];

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

    public string DayStartedText => _dayStarted ? "End Day" : "Start Day";

    public MainWindowViewModel()
    {
        var days = Enum.GetValues<DayOfWeek>();
        foreach (var day in days)
            DaysOfWeekSelection[day] = false;

        SyncTaskTypeFlags();
        CurrentTask.PropertyChanged += OnCurrentTaskPropertyChanged;

        PropertyChanged += OnViewModelPropertyChanged;
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CurrentTask))
        {
            CurrentTask.PropertyChanged -= OnCurrentTaskPropertyChanged;
            CurrentTask.PropertyChanged += OnCurrentTaskPropertyChanged;
            SyncTaskTypeFlags();
        }
    }

    void OnCurrentTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TaskTemplate.Type))
            SyncTaskTypeFlags();
    }

    void SyncTaskTypeFlags()
    {
        IsRecurring = CurrentTask.Type == TaskType.Recurring;
        IsFixedTime = CurrentTask.Type == TaskType.FixedTime;
        IsOneTime = CurrentTask.Type == TaskType.OneTime;
        ShowDaysOfWeek = CurrentTask.Type != TaskType.OneTime;
        ShowDuration = CurrentTask.Type == TaskType.Recurring || CurrentTask.Type == TaskType.Things;
        IsRecurringOrThings = CurrentTask.Type == TaskType.Recurring || CurrentTask.Type == TaskType.Things;
    }

    [RelayCommand]
    void OpenNewTask()
    {
        CurrentTask = new();
        CurrentTask.Type = TaskType.Recurring;
        foreach (var day in DaysOfWeekSelection.Keys)
            DaysOfWeekSelection[day] = false;
        IsNewTaskOpen = true;
    }

    [RelayCommand]
    void OnTaskTypeChanged(TaskType type)
    {
        foreach (var day in DaysOfWeekSelection.Keys)
            DaysOfWeekSelection[day] = false;
    }

    [RelayCommand]
    void SaveNewTask()
    {
        if (string.IsNullOrWhiteSpace(CurrentTask.Name))
            return;

        var task = new TaskTemplate
        {
            Id = CurrentTask.Id,
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

        IsNewTaskOpen = false;
    }

    [RelayCommand]
    void CancelNewTask()
    {
        IsNewTaskOpen = false;
    }

    [RelayCommand]
    void ToggleDay()
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
    void FinishCurrentTask()
    {
    }
}
