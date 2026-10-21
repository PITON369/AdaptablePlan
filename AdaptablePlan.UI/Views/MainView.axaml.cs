using AdaptablePlan.UI.Models;
using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AdaptablePlan.UI.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void OnCellButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ScheduleItem item } && DataContext is MainWindowViewModel vm)
            vm.SelectedItem = item;
    }
}
