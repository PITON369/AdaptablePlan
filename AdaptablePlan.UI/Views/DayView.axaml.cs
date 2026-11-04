using AdaptablePlan.UI.Models;
using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AdaptablePlan.UI.Views;

public partial class DayView : UserControl
{
    public DayView()
    {
        InitializeComponent();
    }

    private async void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: ScheduleItem item }
            && DataContext is MainWindowViewModel vm)
            await vm.ToggleDone(item);
    }
}
