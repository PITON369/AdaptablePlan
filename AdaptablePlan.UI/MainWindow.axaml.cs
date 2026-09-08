using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AdaptablePlan.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnCellButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ScheduleItem item } && DataContext is MainWindowViewModel vm)
            vm.SelectedItem = item;
    }
}
