using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Models;
using AdaptablePlan.UI.Localization;
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

    // Для ComboBox в режиме Custom: только слоты 1..10. Год (Slot 0) —
    // отдельный основной режим (радио Year, вьюха YearView), а не Custom.
    public ObservableCollection<CustomPageViewModel> CustomPageList { get; } = new();

    public CustomPageViewModel? YearPage { get; private set; }

    // Служебная страница чек-листа планов месяца (Slot = -1), как Year (Slot 0).
    public CustomPageViewModel? MonthPage { get; private set; }

    [ObservableProperty]
    private CustomPageViewModel? _selectedCustomPage;

    // Custom-режим: true — редактор страницы, false — отчёт (шаблон вида с
    // данными страницы). Пустая страница и Note всегда открываются в редакторе.
    [ObservableProperty]
    private bool _isCustomEditor = true;

    private static bool ShouldEdit(CustomPageViewModel? page)
        => page == null
           || page.Model.ViewType == PageViewType.Note
           || page.Entries.Count == 0;

    [ObservableProperty]
    private string _newCustomEntryText = string.Empty;

    [ObservableProperty]
    private string _newCustomEntryStart = string.Empty;

    [ObservableProperty]
    private string _newCustomEntryEnd = string.Empty;

    // День новой записи (для вида Week), по умолчанию — сегодня.
    [ObservableProperty]
    private DayOfWeek _newCustomEntryDay = DateTime.Today.DayOfWeek;

    // Порядок дней в комбобоксе — с понедельника.
    public DayOfWeek[] WeekDays { get; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
         DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    [ObservableProperty]
    private string _newChecklistText = string.Empty;

    partial void OnSelectedCustomPageChanged(CustomPageViewModel? value)
    {
        IsCustomEditor = ShouldEdit(value);
        OnPropertyChanged(nameof(CustomConflictText));
        OnPropertyChanged(nameof(ShowTaskButton));
        OnPropertyChanged(nameof(ChecklistEntries));
        OnPropertyChanged(nameof(ChecklistTitle));
        RegenerateSchedule();
    }

    partial void OnIsCustomEditorChanged(bool value) => RegenerateSchedule();

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
            new() { Id = Guid.NewGuid(), Slot = -1, Name = "Month", ViewType = PageViewType.List },
        };
        for (int i = 1; i <= 10; i++)
            pages.Add(new CustomPage { Id = Guid.NewGuid(), Slot = i, Name = $"Custom {i}", ViewType = types[i - 1] });
        return pages;
    }

    private void LoadCustomPages(IAdaptablePlanDb db)
    {
        var pages = db.CustomPages.GetAllAsync().GetAwaiter().GetResult().ToList();
        if (pages.Count == 0)
        {
            pages = DefaultCustomPages();
            foreach (var p in pages)
                db.CustomPages.InsertAsync(p).GetAwaiter().GetResult();
        }
        // Страница месяца добавлена позже дефолтных — досеваем при отсутствии.
        else if (pages.All(p => p.Slot != -1))
        {
            var month = DefaultCustomPages().First(p => p.Slot == -1);
            db.CustomPages.InsertAsync(month).GetAwaiter().GetResult();
            pages.Add(month);
        }
        ApplyCustomPages(pages);
    }

    private void ApplyCustomPages(IEnumerable<CustomPage> pages)
    {
        CustomPages.Clear();
        CustomPageList.Clear();
        YearPage = null;
        MonthPage = null;
        foreach (var p in pages.OrderBy(p => p.Slot))
        {
            var vm = new CustomPageViewModel(p, SaveCustomPage);
            // Смена вида страницы влияет на кнопку Start Task и на выбор
            // редактор/отчёт (Note всегда редактор).
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(CustomPageViewModel.IsDayView))
                    return;
                OnPropertyChanged(nameof(ShowTaskButton));
                IsCustomEditor = ShouldEdit(vm);
                OnPropertyChanged(nameof(ChecklistEntries));
                OnPropertyChanged(nameof(ChecklistTitle));
            };
            if (p.Slot == 0)
                YearPage = vm;
            else if (p.Slot == -1)
                MonthPage = vm;
            else
                CustomPages.Add(vm);
        }

        foreach (var page in CustomPages)
            CustomPageList.Add(page);

        OnPropertyChanged(nameof(YearPage));
        OnPropertyChanged(nameof(MonthPage));
        SelectedCustomPage = CustomPages.FirstOrDefault();
    }

    private void SaveCustomPage(CustomPageViewModel page)
    {
        _db?.CustomPages.UpdateAsync(page.Model).GetAwaiter().GetResult();
    }

    // Save: сохранить страницу и переключиться на отчёт (Note остаётся
    // редактором — там показ и редактирование совмещены).
    [RelayCommand]
    private void SaveSelectedPage()
    {
        if (SelectedCustomPage == null)
            return;
        SelectedCustomPage.Save();
        if (SelectedCustomPage.Model.ViewType != PageViewType.Note)
            IsCustomEditor = false;
    }

    [RelayCommand]
    private void EditCustomPage() => IsCustomEditor = true;

    // Чек-лист (шаблон YearView): в режимах Month/Year — служебные страницы
    // месяца/года, в отчёте Custom-страницы видов List/Year — записи страницы.
    public string ChecklistTitle
        => Mode == ScheduleMode.Month ? Strings.Get("PlansForMonth")
         : Mode == ScheduleMode.Year ? Strings.Get("PlansForYear")
         : SelectedCustomPage?.DisplayName ?? string.Empty;

    public ObservableCollection<CustomEntryViewModel>? ChecklistEntries
        => Mode == ScheduleMode.Month ? MonthPage?.Entries
         : Mode == ScheduleMode.Year ? YearPage?.Entries
         : Mode == ScheduleMode.Custom ? SelectedCustomPage?.Entries
         : null;

    [RelayCommand]
    private void AddChecklistEntry()
    {
        var name = NewChecklistText.Trim();
        if (name.Length == 0)
            return;
        if (Mode == ScheduleMode.Month && MonthPage != null)
            MonthPage.AddEntry(name, string.Empty, string.Empty, DateTime.Today.DayOfWeek);
        else if (Mode == ScheduleMode.Year && YearPage != null)
            YearPage.AddEntry(name, string.Empty, string.Empty, DateTime.Today.DayOfWeek);
        else if (Mode == ScheduleMode.Custom && SelectedCustomPage != null)
            SelectedCustomPage.AddEntry(name, string.Empty, string.Empty, DateTime.Today.DayOfWeek);
        NewChecklistText = string.Empty;
    }

    // Очистка возвращает страницу в редактор — данные пусты, показывать отчёт нечего.
    [RelayCommand]
    private void ClearSelectedPage()
    {
        if (SelectedCustomPage == null)
            return;
        SelectedCustomPage.Clear();
        IsCustomEditor = ShouldEdit(SelectedCustomPage);
    }

    [RelayCommand]
    private void AddCustomEntry()
    {
        var name = NewCustomEntryText.Trim();
        if (SelectedCustomPage == null || name.Length == 0)
            return;
        // День из комбобокса нужен виду Week; в остальных видах день не важен.
        var day = SelectedCustomPage.IsWeekView ? NewCustomEntryDay : DateTime.Today.DayOfWeek;
        SelectedCustomPage.AddEntry(name, NewCustomEntryStart.Trim(), NewCustomEntryEnd.Trim(), day);
        NewCustomEntryText = string.Empty;
        NewCustomEntryStart = string.Empty;
        NewCustomEntryEnd = string.Empty;
    }

    [RelayCommand]
    private void RemoveCustomEntry(CustomEntryViewModel? entry)
    {
        if (entry == null)
            return;
        // Страница-владелец известна записи; ищем её среди слотов, Month и Year.
        (MonthPage != null && MonthPage.Entries.Contains(entry) ? MonthPage
            : YearPage != null && YearPage.Entries.Contains(entry) ? YearPage
            : CustomPages.FirstOrDefault(p => p.Entries.Contains(entry)))?.RemoveEntry(entry);
        OnPropertyChanged(nameof(CustomConflictText));
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
                : Strings.Get("ConflictsWith") + string.Join("; ", conflicts.Distinct());
        }
    }
}
