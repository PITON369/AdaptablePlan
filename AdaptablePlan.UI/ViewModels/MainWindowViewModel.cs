using System.Collections.ObjectModel;
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

    [ObservableProperty]
    private bool _isNewTaskOpen;

    [ObservableProperty]
    private ScheduleEntry _newTaskEntry = new();

    [RelayCommand]
    void OpenNewTask()
    {
        NewTaskEntry = new();
        IsNewTaskOpen = true;
    }

    [RelayCommand]
    void SaveNewTask()
    {
        if (!string.IsNullOrWhiteSpace(NewTaskEntry.Activity))
        {
            Schedule.Add(NewTaskEntry);
            IsNewTaskOpen = false;
        }
    }

    [RelayCommand]
    void CancelNewTask()
    {
        IsNewTaskOpen = false;
    }
}
