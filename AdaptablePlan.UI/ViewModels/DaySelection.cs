using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace AdaptablePlan.UI.ViewModels;

public partial class DaySelection : ObservableObject
{
    public DayOfWeek Day { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}
