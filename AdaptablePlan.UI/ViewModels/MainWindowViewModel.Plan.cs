using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Localization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AdaptablePlan.UI.ViewModels;

// Экран "Планы" (цели и задачи): общие план месяца и план года,
// показываются сразу в режимах Month и Year.
public partial class MainWindowViewModel
{
    public PlanPageViewModel? MonthPlan { get; private set; }
    public PlanPageViewModel? YearPlan { get; private set; }

    public PlanPageViewModel? CurrentPlan
        => Mode == ScheduleMode.Month ? MonthPlan : YearPlan;

    // Заголовок плана: "План месяца ноябрь" / "План года 2026"
    // (по выбранной дате). Сетка недель плана месяца тоже следует за ней.
    public string CurrentPlanTitle
    {
        get
        {
            if (CurrentPlan == null)
                return string.Empty;
            return CurrentPlan == MonthPlan
                ? Strings.Format("PlanMonthName",
                    SelectedDate.ToString("MMMM", new CultureInfo(Strings.Language)))
                : Strings.Format("PlanYearName", SelectedDate.Year);
        }
    }

    // Строки сетки недель плана месяца - реальные недели месяца SelectedDate,
    // дни вне месяца скрыты.
    public IReadOnlyList<PlanMonthWeekViewModel> PlanWeeks
    {
        get
        {
            if (_monthGridWeeks == null)
                RebuildMonthPlanGrid();
            return _monthGridWeeks!;
        }
    }

    private List<PlanMonthWeekViewModel> _monthGridWeeks = new();

    private void RebuildMonthPlanGrid()
    {
        var monthList = new List<PlanMonthWeekViewModel>();
        var plan = MonthPlan;
        if (plan != null)
        {
            var first = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
            // Сдвиг 1-го числа от понедельника (сетка всегда Пн..Вс).
            var offset = ((int)first.DayOfWeek + 6) % 7;
            var daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);
            var weekCount = (offset + daysInMonth + 6) / 7;
            var gridStart = first.AddDays(-offset);
            for (var w = 1; w <= weekCount; w++)
            {
                var modelWeek = EnsureWeek(plan.Model, w);
                var days = new List<PlanMonthDayViewModel>();
                for (var i = 0; i < 7; i++)
                {
                    var date = gridStart.AddDays(7 * (w - 1) + i);
                    if (date.Month != first.Month)
                    {
                        days.Add(new PlanMonthDayViewModel(null, null, null));
                        continue;
                    }
                    var cell = modelWeek.Days.FirstOrDefault(c => c.DayIndex == i);
                    if (cell == null)
                    {
                        cell = new PlanDayCell { DayIndex = i };
                        modelWeek.Days.Add(cell);
                    }
                    days.Add(new PlanMonthDayViewModel(date, cell, plan));
                }
                monthList.Add(new PlanMonthWeekViewModel(w, days));
            }
        }
        _monthGridWeeks = monthList;
        OnPropertyChanged(nameof(PlanWeeks));
    }

    // Недостающие недели сетки (5-я, 6-я в длинных месяцах) дозаводятся
    // в модели; сохранятся вместе со страницей при первой правке данных.
    private static PlanWeek EnsureWeek(PlanPage model, int number)
    {
        var week = model.Weeks.FirstOrDefault(w => w.Number == number);
        if (week != null)
            return week;
        week = new PlanWeek
        {
            Number = number,
            Days = Enumerable.Range(0, 7).Select(i => new PlanDayCell { DayIndex = i }).ToList(),
        };
        model.Weeks.Add(week);
        return week;
    }

    private void LoadPlanPages(IAdaptablePlanDb db)
    {
        var pages = db.PlanPages.GetAllAsync().GetAwaiter().GetResult().ToList();
        foreach (var kind in new[] { PlanPageKind.Month, PlanPageKind.Year })
        {
            if (pages.Any(p => p.Kind == kind))
                continue;
            var page = DefaultPlanPages().First(p => p.Kind == kind);
            db.PlanPages.InsertAsync(page).GetAwaiter().GetResult();
            pages.Add(page);
        }
        ApplyPlanPages(pages);
    }

    private void ApplyPlanPages(IEnumerable<PlanPage> pages)
    {
        MonthPlan = null;
        YearPlan = null;
        foreach (var p in pages)
        {
            if (p.Kind == PlanPageKind.Month)
                MonthPlan = new PlanPageViewModel(p, SavePlanPage);
            else
                YearPlan = new PlanPageViewModel(p, SavePlanPage);
        }
        OnPropertyChanged(nameof(MonthPlan));
        OnPropertyChanged(nameof(YearPlan));
        OnPropertyChanged(nameof(CurrentPlan));
        RebuildMonthPlanGrid();
    }

    private void SavePlanPage(PlanPageViewModel page)
    {
        _db?.PlanPages.UpdateAsync(page.Model).GetAwaiter().GetResult();
    }

    private static List<PlanPage> DefaultPlanPages() =>
        new[] { PlanPageKind.Month, PlanPageKind.Year }.Select(kind => new PlanPage
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Spheres = new (string LocKey, string Name)[]
                {
                    ("SphereProfession", "Профессия"),
                    ("SphereSport", "Спорт"),
                    ("SphereLanguage", "Язык"),
                }
                .Select(t => new PlanSphere { Id = Guid.NewGuid(), LocKey = t.LocKey, Name = t.Name })
                .ToList(),
            Weeks = Enumerable.Range(1, 4).Select(n => new PlanWeek
            {
                Number = n,
                Days = Enumerable.Range(0, 7)
                    .Select(d => new PlanDayCell { DayIndex = d })
                    .ToList(),
            }).ToList(),
        }).ToList();
}
