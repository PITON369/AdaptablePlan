using AdaptablePlan.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AdaptablePlan.UI.ViewModels;

// Обёртка CustomPage для UI. Вид фиксирован, данные меняются через свойства.
public partial class CustomPageViewModel : ObservableObject
{
    private readonly Action<CustomPageViewModel> _save;

    public CustomPageViewModel(CustomPage model, Action<CustomPageViewModel> save)
    {
        Model = model;
        _save = save;
        Entries = new ObservableCollection<CustomEntryViewModel>(
            model.Entries.Select(e => new CustomEntryViewModel(e, this)));
    }

    public CustomPage Model { get; }
    public int Slot => Model.Slot;

    public string Name
    {
        get => Model.Name;
        set { Model.Name = value; OnPropertyChanged(); }
    }

    public bool IsDayView => Model.ViewType == PageViewType.Day;
    public bool IsWeekView => Model.ViewType == PageViewType.Week;
    public bool IsMonthView => Model.ViewType == PageViewType.Month;
    public bool IsNoteView => Model.ViewType == PageViewType.Note;

    // Чек-лист — общий UI для видов List и Year.
    public bool IsChecklistView => Model.ViewType is PageViewType.List or PageViewType.Year;

    // Порядок показа в ComboBox — естественный порядок enum'а.
    public IEnumerable<PageViewType> ViewTypes => Enum.GetValues<PageViewType>();

    public PageViewType ViewType
    {
        get => Model.ViewType;
        set
        {
            if (Model.ViewType == value)
                return;
            Model.ViewType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDayView));
            OnPropertyChanged(nameof(IsWeekView));
            OnPropertyChanged(nameof(IsMonthView));
            OnPropertyChanged(nameof(IsChecklistView));
            OnPropertyChanged(nameof(IsNoteView));
            Save();
        }
    }

    public ObservableCollection<CustomEntryViewModel> Entries { get; }

    public string NoteText
    {
        get => Model.NoteText;
        set { Model.NoteText = value; OnPropertyChanged(); }
    }

    public void Save() => _save(this);

    public void AddEntry(string name, string start, string end)
    {
        var entry = new CustomEntry
        {
            Id = Guid.NewGuid(),
            Name = name,
            Day = DateTime.Today.DayOfWeek,
            StartTime = start,
            EndTime = end,
        };
        Model.Entries.Add(entry);
        Entries.Add(new CustomEntryViewModel(entry, this));
        Save();
    }

    public void RemoveEntry(CustomEntryViewModel entry)
    {
        Model.Entries.Remove(entry.Model);
        Entries.Remove(entry);
        Save();
    }
}

public partial class CustomEntryViewModel : ObservableObject
{
    private readonly CustomPageViewModel _owner;

    public CustomEntryViewModel(CustomEntry model, CustomPageViewModel owner)
    {
        Model = model;
        _owner = owner;
    }

    public CustomEntry Model { get; }

    public string Name
    {
        get => Model.Name;
        set { Model.Name = value; OnPropertyChanged(); }
    }

    public string StartTime
    {
        get => Model.StartTime;
        set { Model.StartTime = value; OnPropertyChanged(); }
    }

    public string EndTime
    {
        get => Model.EndTime;
        set { Model.EndTime = value; OnPropertyChanged(); }
    }

    public string DayName => Model.Day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        _ => "Sun",
    };

    public bool IsDone
    {
        get => Model.IsDone;
        set
        {
            Model.IsDone = value;
            OnPropertyChanged();
            _owner.Save();
        }
    }
}
