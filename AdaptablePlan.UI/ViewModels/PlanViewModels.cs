using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AdaptablePlan.UI.ViewModels;

// Обёртка PlanPage для UI: секции-чеклисты, сферы, сетка недель.
// Любое изменение данных автосохраняется через колбэк Save.
public partial class PlanPageViewModel : ObservableObject
{
    private readonly Action<PlanPageViewModel> _save;

    public PlanPageViewModel(PlanPage model, Action<PlanPageViewModel> save)
    {
        Model = model;
        _save = save;
        Title = Strings.Get(model.Kind == PlanPageKind.Month ? "PlanMonth" : "PlanYear");
        Learn = new PlanListViewModel(model.Learn, Save);
        Improve = new PlanListViewModel(model.Improve, Save);
        Try = new PlanListViewModel(model.Try, Save);
        Achieve = new PlanListViewModel(model.Achieve, Save);
        Articles = new PlanListViewModel(model.Articles, Save);
        Courses = new PlanListViewModel(model.Courses, Save);
        Events = new PlanListViewModel(model.Events, Save);
        Spheres = new ObservableCollection<PlanSphereViewModel>(
            model.Spheres.Select(s => new PlanSphereViewModel(s, this)));
        Weeks = new ObservableCollection<PlanWeekViewModel>(
            model.Weeks.OrderBy(w => w.Number).Select(w => new PlanWeekViewModel(w, this)));
    }

    public PlanPage Model { get; }
    public string Title { get; }

    public PlanListViewModel Learn { get; }
    public PlanListViewModel Improve { get; }
    public PlanListViewModel Try { get; }
    public PlanListViewModel Achieve { get; }
    public PlanListViewModel Articles { get; }
    public PlanListViewModel Courses { get; }
    public PlanListViewModel Events { get; }

    public ObservableCollection<PlanSphereViewModel> Spheres { get; }
    public ObservableCollection<PlanWeekViewModel> Weeks { get; }

    public void Save() => _save(this);

    [RelayCommand]
    private void AddSphere()
    {
        var sphere = new PlanSphere { Id = Guid.NewGuid() };
        Model.Spheres.Add(sphere);
        Spheres.Add(new PlanSphereViewModel(sphere, this));
        Save();
    }

    [RelayCommand]
    internal void RemoveSphere(PlanSphereViewModel? sphere)
    {
        if (sphere == null)
            return;
        Model.Spheres.Remove(sphere.Model);
        Spheres.Remove(sphere);
        Save();
    }
}

// Чек-лист одной секции плана: пункты + поле добавления.
public partial class PlanListViewModel : ObservableObject
{
    private readonly Action _save;
    private readonly List<PlanEntry> _model;

    public PlanListViewModel(List<PlanEntry> model, Action save)
    {
        _model = model;
        _save = save;
        Items = new ObservableCollection<PlanEntryViewModel>(
            model.Select(e => new PlanEntryViewModel(e, this)));
    }

    public ObservableCollection<PlanEntryViewModel> Items { get; }

    [ObservableProperty]
    private string _newItemText = string.Empty;

    [RelayCommand]
    private void Add()
    {
        var name = NewItemText.Trim();
        if (name.Length == 0)
            return;
        var entry = new PlanEntry { Id = Guid.NewGuid(), Name = name };
        _model.Add(entry);
        Items.Add(new PlanEntryViewModel(entry, this));
        NewItemText = string.Empty;
        _save();
    }

    internal void Remove(PlanEntryViewModel entry)
    {
        _model.Remove(entry.Model);
        Items.Remove(entry);
        _save();
    }

    internal void Save() => _save();
}

public partial class PlanEntryViewModel : ObservableObject
{
    private readonly PlanListViewModel _owner;

    public PlanEntryViewModel(PlanEntry model, PlanListViewModel owner)
    {
        Model = model;
        _owner = owner;
    }

    public PlanEntry Model { get; }

    public string Name => Model.Name;

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

    [RelayCommand]
    private void Remove() => _owner.Remove(this);
}

// Сфера с вложенным списком пунктов.
public partial class PlanSphereViewModel : ObservableObject
{
    private readonly PlanPageViewModel _owner;

    public PlanSphereViewModel(PlanSphere model, PlanPageViewModel owner)
    {
        Model = model;
        _owner = owner;
        Items = new PlanListViewModel(model.Items, () => _owner.Save());
    }

    public PlanSphere Model { get; }

    public string Name
    {
        get => Model.Name;
        set { Model.Name = value; OnPropertyChanged(); _owner.Save(); }
    }

    public PlanListViewModel Items { get; }

    [RelayCommand]
    private void RemoveSelf() => _owner.RemoveSphere(this);
}

// Неделя сетки: заголовок и 7 ячеек по дням Пн..Вс.
public class PlanWeekViewModel
{
    private readonly PlanPageViewModel _owner;

    public PlanWeekViewModel(PlanWeek model, PlanPageViewModel owner)
    {
        _owner = owner;
        Title = Strings.Format("WeekN", model.Number);
        Days = new ObservableCollection<PlanDayCellViewModel>(
            model.Days.OrderBy(d => d.DayIndex).Select(d => new PlanDayCellViewModel(d, this)));
    }

    public string Title { get; }
    public ObservableCollection<PlanDayCellViewModel> Days { get; }

    internal void Save() => _owner.Save();
}

public class PlanDayCellViewModel
{
    private readonly PlanWeekViewModel _week;

    public PlanDayCellViewModel(PlanDayCell model, PlanWeekViewModel week)
    {
        Model = model;
        _week = week;
        Items = new PlanListViewModel(model.Items, () => _week.Save());
    }

    public PlanDayCell Model { get; }

    public string DayName => Model.DayIndex switch
    {
        0 => Strings.Get("Mon"),
        1 => Strings.Get("Tue"),
        2 => Strings.Get("Wed"),
        3 => Strings.Get("Thu"),
        4 => Strings.Get("Fri"),
        5 => Strings.Get("Sat"),
        _ => Strings.Get("Sun"),
    };

    public PlanListViewModel Items { get; }
}
