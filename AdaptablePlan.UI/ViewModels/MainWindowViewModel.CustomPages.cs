using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AdaptablePlan.UI.ViewModels;

// Страницы Custom (слоты 1..10) и служебная страница Year (Slot 0).
public partial class MainWindowViewModel
{
    public ObservableCollection<CustomPageViewModel> CustomPages { get; } = new();

    // Для ComboBox: Year (Slot 0) + все слоты 1..10.
    public ObservableCollection<CustomPageViewModel> CustomPageList { get; } = new();

    public CustomPageViewModel? YearPage { get; private set; }

    [ObservableProperty]
    private CustomPageViewModel? _selectedCustomPage;

    [ObservableProperty]
    private string _newCustomEntryText = string.Empty;

    [ObservableProperty]
    private string _newCustomEntryStart = string.Empty;

    [ObservableProperty]
    private string _newCustomEntryEnd = string.Empty;

    [ObservableProperty]
    private string _newYearEntryText = string.Empty;

    partial void OnSelectedCustomPageChanged(CustomPageViewModel? value)
        => OnPropertyChanged(nameof(CustomConflictText));

    // Страница 10 слотов + Year (Slot 0). Виды по умолчанию: первые пять —
    // по одному каждого вида, остальные — списки.
    private static List<CustomPage> DefaultCustomPages()
    {
        PageViewType[] types =
            [PageViewType.Day, PageViewType.Week, PageViewType.Month, PageViewType.List, PageViewType.Note,
             PageViewType.List, PageViewType.List, PageViewType.List, PageViewType.List, PageViewType.List];

        var pages = new List<CustomPage>
        {
            new() { Id = Guid.NewGuid(), Slot = 0, Name = "Year", ViewType = PageViewType.Year },
        };
        for (int i = 1; i <= 10; i++)
            pages.Add(new CustomPage { Id = Guid.NewGuid(), Slot = i, Name = $"Custom {i}", ViewType = types[i - 1] });
        return pages;
    }

    private void LoadCustomPages(IAdaptablePlanDb db)
    {
        var pages = db.CustomPages.GetAllAsync().GetAwaiter().GetResult();
        if (pages.Count == 0)
        {
            pages = DefaultCustomPages();
            foreach (var p in pages)
                db.CustomPages.InsertAsync(p).GetAwaiter().GetResult();
        }
        ApplyCustomPages(pages);
    }

    private void ApplyCustomPages(IEnumerable<CustomPage> pages)
    {
        CustomPages.Clear();
        CustomPageList.Clear();
        YearPage = null;
        foreach (var p in pages.OrderBy(p => p.Slot))
        {
            var vm = new CustomPageViewModel(p, SaveCustomPage);
            if (p.Slot == 0)
                YearPage = vm;
            else
                CustomPages.Add(vm);
        }

        foreach (var page in CustomPages)
            CustomPageList.Add(page);

        OnPropertyChanged(nameof(YearPage));
        SelectedCustomPage = CustomPages.FirstOrDefault();
    }

    private void SaveCustomPage(CustomPageViewModel page)
    {
        _db?.CustomPages.UpdateAsync(page.Model).GetAwaiter().GetResult();
    }

    [RelayCommand]
    private void SaveSelectedPage() => SelectedCustomPage?.Save();

    [RelayCommand]
    private void AddCustomEntry()
    {
        var name = NewCustomEntryText.Trim();
        if (SelectedCustomPage == null || name.Length == 0)
            return;
        SelectedCustomPage.AddEntry(name, NewCustomEntryStart.Trim(), NewCustomEntryEnd.Trim());
        NewCustomEntryText = string.Empty;
        NewCustomEntryStart = string.Empty;
        NewCustomEntryEnd = string.Empty;
    }

    [RelayCommand]
    private void RemoveCustomEntry(CustomEntryViewModel? entry)
    {
        if (entry == null)
            return;
        // Страница-владелец известна записи; ищем её среди слотов и Year.
        (YearPage != null && YearPage.Entries.Contains(entry) ? YearPage
            : CustomPages.FirstOrDefault(p => p.Entries.Contains(entry)))?.RemoveEntry(entry);
        OnPropertyChanged(nameof(CustomConflictText));
    }

    [RelayCommand]
    private void AddYearEntry()
    {
        var name = NewYearEntryText.Trim();
        if (YearPage == null || name.Length == 0)
            return;
        YearPage.AddEntry(name, string.Empty, string.Empty);
        NewYearEntryText = string.Empty;
    }

    // Пересечение записей выбранной Custom-страницы с пунктами основного
    // расписания на выбранную дату. Для Week сравниваются записи этого дня недели.
    public string CustomConflictText
    {
        get
        {
            var page = SelectedCustomPage;
            if (page == null || Mode != ScheduleMode.Custom)
                return string.Empty;
            if (page.Model.ViewType is PageViewType.List or PageViewType.Note or PageViewType.Month or PageViewType.Year)
                return string.Empty;

            var pageEntries = page.Model.ViewType == PageViewType.Week
                ? page.Model.Entries.Where(e => e.Day == SelectedDate.DayOfWeek)
                : page.Model.Entries;

            var conflicts = new List<string>();
            foreach (var e in pageEntries)
            {
                if (!ScheduleExpander.TryParseTime(e.StartTime, out var ps)
                    || !ScheduleExpander.TryParseTime(e.EndTime, out var pe))
                    continue;
                foreach (var item in Schedule)
                {
                    if (!ScheduleExpander.TryParseTime(item.StartTime, out var is2)
                        || !ScheduleExpander.TryParseTime(item.EndTime, out var ie))
                        continue;
                    if (ps < ie && is2 < pe)
                        conflicts.Add($"{item.StartTime}-{item.EndTime} {item.Activity}");
                }
            }

            return conflicts.Count == 0
                ? string.Empty
                : "Conflicts with schedule: " + string.Join("; ", conflicts.Distinct());
        }
    }
}
