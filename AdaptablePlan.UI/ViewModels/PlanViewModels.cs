using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;

namespace AdaptablePlan.UI.ViewModels;

// Обёртка PlanPage для UI: секции-чеклисты, сферы, сетка недель.
// Любое изменение данных автосохраняется через колбэк Save.
public partial class PlanPageViewModel : ObservableObject
{
    private static readonly string[] DefaultSphereLocKeys =
        { "SphereProfession", "SphereSport", "SphereLanguage" };

    private readonly Action<PlanPageViewModel> _save;

    public PlanPageViewModel(PlanPage model, Action<PlanPageViewModel> save)
    {
        Model = model;
        _save = save;
        // Страницам, созданным до LocKey, ключ проставляется по совпадению имени
        // с дефолтной сферой (любого языка).
        foreach (var sphere in model.Spheres)
            if (sphere.LocKey.Length == 0)
                sphere.LocKey = DefaultSphereLocKeys
                    .FirstOrDefault(k => Strings.Get(k) == sphere.Name) ?? "";
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
        for (var m = 1; m <= 12; m++)
            if (!model.Months.Any(c => c.Month == m))
                model.Months.Add(new PlanMonthCell { Month = m });
        Months = new ObservableCollection<PlanYearMonthViewModel>(
            model.Months.OrderBy(c => c.Month).Select(c => new PlanYearMonthViewModel(c, this)));
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
    public ObservableCollection<PlanYearMonthViewModel> Months { get; }

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
        get => Model.LocKey.Length > 0 ? Strings.Get(Model.LocKey) : Model.Name;
        set
        {
            // Запись в TextBox неизменённого локализованного имени (потеря фокуса)
            // не должна отнимать локализацию.
            if (Model.LocKey.Length > 0 && value == Strings.Get(Model.LocKey))
                return;
            Model.Name = value;
            Model.LocKey = "";
            OnPropertyChanged();
            _owner.Save();
        }
    }

    public PlanListViewModel Items { get; }

    [RelayCommand]
    private void RemoveSelf() => _owner.RemoveSphere(this);
}

// Ячейка месяца в годовом плане: заголовок и свой список пунктов.
public class PlanYearMonthViewModel
{
    public PlanYearMonthViewModel(PlanMonthCell model, PlanPageViewModel page)
    {
        MonthName = new DateTime(2000, model.Month, 1)
            .ToString("MMMM", new CultureInfo(Strings.Language));
        Items = new PlanListViewModel(model.Items, () => page.Save());
    }

    public string MonthName { get; }

    public PlanListViewModel Items { get; }
}

// Неделя реального календаря выбранного месяца: 7 слотов Пн..Вс, у дней
// вне месяца содержимое пустое.
public class PlanMonthWeekViewModel
{
    public PlanMonthWeekViewModel(int number, List<PlanMonthDayViewModel> days)
    {
        Title = Strings.Format("WeekN", number);
        Days = days;
    }

    public string Title { get; }
    public List<PlanMonthDayViewModel> Days { get; }
}

// Ячейка дня сетки плана месяца с датой ("2 ноя") либо пустой слот.
public class PlanMonthDayViewModel
{
    private readonly DateTime? _date;
    private readonly PlanPageViewModel? _page;

    public PlanMonthDayViewModel(DateTime? date, PlanDayCell? cell, PlanPageViewModel? page)
    {
        _date = date;
        _page = page;
        // Пустой слот дня вне месяца: список неотличим от обычного, чтобы
        // привязки в шаблоне не падали, - редактировать его нельзя, ячейка
        // скрыта по IsReal.
        Items = date == null || cell == null || page == null
            ? new PlanListViewModel(new List<PlanEntry>(), () => { })
            : new PlanListViewModel(cell.Items, () => page.Save());
    }

    public bool IsReal => _date != null;

    public string DayName => _date == null
        ? string.Empty
        : _date.Value.DayOfWeek switch
        {
            DayOfWeek.Monday => Strings.Get("Mon"),
            DayOfWeek.Tuesday => Strings.Get("Tue"),
            DayOfWeek.Wednesday => Strings.Get("Wed"),
            DayOfWeek.Thursday => Strings.Get("Thu"),
            DayOfWeek.Friday => Strings.Get("Fri"),
            DayOfWeek.Saturday => Strings.Get("Sat"),
            _ => Strings.Get("Sun"),
        };

    public string DateText => _date == null
        ? string.Empty
        : _date.Value.ToString("d MMM", new CultureInfo(Strings.Language));

    public PlanListViewModel Items { get; }
}
