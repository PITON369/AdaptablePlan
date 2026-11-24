using Avalonia.Controls;
using Avalonia.Input;

namespace AdaptablePlan.UI.Views;

public partial class NewTaskEntryView : UserControl
{
    public NewTaskEntryView()
    {
        InitializeComponent();
    }

    private void OnTimeTextInput(object? sender, TextInputEventArgs e) => TimeBox.OnTextInput(sender, e);

    private void OnTimeTextChanged(object? sender, TextChangedEventArgs e) => TimeBox.OnTextChanged(sender, e);

    private void OnDigitsTextInput(object? sender, TextInputEventArgs e) => DigitsBox.OnTextInput(sender, e);

    private void OnDigitsTextChanged(object? sender, TextChangedEventArgs e) => DigitsBox.OnTextChanged(sender, e);
}
