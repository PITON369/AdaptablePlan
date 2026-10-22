using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AdaptablePlan.UI.Views;

public partial class CustomPageView : UserControl
{
    public CustomPageView()
    {
        InitializeComponent();
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CustomEntryViewModel entry } && DataContext is MainWindowViewModel vm)
            vm.RemoveCustomEntryCommand.Execute(entry);
    }
}
