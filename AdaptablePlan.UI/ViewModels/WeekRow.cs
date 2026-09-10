using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AdaptablePlan.UI.ViewModels;

// TimeLabel обновляется при выборе таски: столбец Time показывает времена
// выбранного дня. DefaultLabel — общий интервал строки по всем дням.
public sealed class WeekRow : ObservableObject
{
    private string _timeLabel = string.Empty;

    public string TimeLabel
    {
        get => _timeLabel;
        set => SetProperty(ref _timeLabel, value);
    }

    public string DefaultLabel { get; set; } = string.Empty;

    public List<WeekCell> Cells { get; } = new();
}
