using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdaptablePlan.UI.ViewModels;

// Экран "Планы" (цели и задачи): общие план месяца и план года,
// открывается кнопкой в режимах Month и Year.
public partial class MainWindowViewModel
{
    [ObservableProperty]
    private bool _isPlanOpen;

    public PlanPageViewModel? MonthPlan { get; private set; }
    public PlanPageViewModel? YearPlan { get; private set; }

    public PlanPageViewModel? CurrentPlan
        => Mode == ScheduleMode.Month ? MonthPlan : YearPlan;

    [RelayCommand]
    private void OpenPlan()
    {
        IsSettingsOpen = false;
        IsPlanOpen = true;
    }

    [RelayCommand]
    private void ClosePlan() => IsPlanOpen = false;

    partial void OnIsPlanOpenChanged(bool value) => OnPropertyChanged(nameof(CurrentPlan));

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
            Spheres = new[] { "Профессия", "Спорт", "Язык" }
                .Select(n => new PlanSphere { Id = Guid.NewGuid(), Name = n })
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
