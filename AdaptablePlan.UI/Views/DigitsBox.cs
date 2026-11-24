using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace AdaptablePlan.UI.Views;

// Маска числового поля (минуты таймера, длительность): только цифры, не более 4.
public static class DigitsBox
{
    public static void OnTextInput(object? sender, TextInputEventArgs e)
        => e.Handled = e.Text?.Any(c => !char.IsDigit(c)) == true;

    public static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.Text is null)
            return;
        var digits = box.Text.Where(char.IsDigit).ToArray();
        if (digits.Length > 4)
            digits = digits[..4];
        var formatted = new string(digits);
        if (formatted == box.Text)
            return;
        var digitsBeforeCaret = box.Text.Take(box.CaretIndex).Count(char.IsDigit);
        box.Text = formatted;
        box.CaretIndex = TimeBox.IndexAfterDigits(formatted, digitsBeforeCaret);
        box.Tag = formatted;
    }
}
