using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AdaptablePlan.UI.Views;

public partial class MonthView : UserControl
{
    public MonthView()
    {
        InitializeComponent();
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CustomEntryViewModel entry } && DataContext is MainWindowViewModel vm)
            vm.RemoveCustomEntryCommand.Execute(entry);
    }
}
