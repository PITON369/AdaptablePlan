using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
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

    private void OnTimeTextInput(object? sender, TextInputEventArgs e) => TimeBox.OnTextInput(sender, e);

    private void OnTimeTextChanged(object? sender, TextChangedEventArgs e) => TimeBox.OnTextChanged(sender, e);
}
