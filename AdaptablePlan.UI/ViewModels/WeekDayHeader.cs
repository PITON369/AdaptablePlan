using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdaptablePlan.UI.ViewModels;

// IsSelected — подсветка заголовка дня, в котором выбрана таска.
public sealed class WeekDayHeader : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public DayOfWeek Day { get; init; }

    // Ширина колонки дня — общая с WeekCell той же колонки.
    public double ColumnWidth { get; set; } = 100;

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
