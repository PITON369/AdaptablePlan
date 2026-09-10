using System;
using System.Linq;
using AdaptablePlan.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdaptablePlan.UI.ViewModels;

// Строка редактора времени в форме задачи: интервал + дни недели.
public partial class TaskTimeVariantViewModel : ObservableObject
{
    [ObservableProperty]
    private TimeSpan? _startValue;

    [ObservableProperty]
    private TimeSpan? _endValue;

    [ObservableProperty]
    private bool _mon;

    [ObservableProperty]
    private bool _tue;

    [ObservableProperty]
    private bool _wed;

    [ObservableProperty]
    private bool _thu;

    [ObservableProperty]
    private bool _fri;

    [ObservableProperty]
    private bool _sat;

    [ObservableProperty]
    private bool _sun;

    public bool HasAnyDay => Mon || Tue || Wed || Thu || Fri || Sat || Sun;

    public static TaskTimeVariantViewModel FromModel(TaskTimeVariant v)
    {
        var vm = new TaskTimeVariantViewModel
        {
            StartValue = Parse(v.StartTime),
            EndValue = Parse(v.EndTime),
        };
        foreach (var day in v.Days)
            vm.SetDay(day, true);
        return vm;
    }

    public TaskTimeVariant ToModel() => new()
    {
        Days = Enum.GetValues<DayOfWeek>().Where(IsSelected).ToHashSet(),
        StartTime = StartValue?.ToString(@"hh\:mm") ?? string.Empty,
        EndTime = EndValue?.ToString(@"hh\:mm") ?? string.Empty,
    };

    private void SetDay(DayOfWeek day, bool value)
    {
        switch (day)
        {
            case DayOfWeek.Monday: Mon = value; break;
            case DayOfWeek.Tuesday: Tue = value; break;
            case DayOfWeek.Wednesday: Wed = value; break;
            case DayOfWeek.Thursday: Thu = value; break;
            case DayOfWeek.Friday: Fri = value; break;
            case DayOfWeek.Saturday: Sat = value; break;
            default: Sun = value; break;
        }
    }

    private bool IsSelected(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Mon,
        DayOfWeek.Tuesday => Tue,
        DayOfWeek.Wednesday => Wed,
        DayOfWeek.Thursday => Thu,
        DayOfWeek.Friday => Fri,
        DayOfWeek.Saturday => Sat,
        _ => Sun,
    };

    private static TimeSpan? Parse(string? text)
        => TimeSpan.TryParse(text, out var ts) ? ts : null;
}
