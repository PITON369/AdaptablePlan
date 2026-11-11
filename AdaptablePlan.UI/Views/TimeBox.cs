using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace AdaptablePlan.UI.Views;

// Маска поля времени "чч:мм": вводятся только цифры, двоеточие всегда
// восстанавливается на третьей позиции - стереть его нельзя.
public static class TimeBox
{
    public static void OnTextInput(object? sender, TextInputEventArgs e)
        => e.Handled = e.Text?.Any(c => !char.IsDigit(c)) == true;

    public static void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.Text is null)
            return;
        var digits = box.Text.Where(char.IsDigit).ToArray();

        // Часа 30-99 не бывает: первый знак 3-9 - это одноциферный час,
        // ведущий ноль подставляем сами ("9" -> "09").
        if (digits.Length > 0 && digits[0] > '2')
            digits = new[] { '0' }.Concat(digits).ToArray();
        if (digits.Length > 4)
            digits = digits[..4];

        // Час 24-29 и минуты 60-99 невозможны. Первый знак минут проверяем
        // сразу (6-9): иначе поле принимает промежуточное "06:6", в которое
        // уже ничего валидного не ввести, и застревает.
        if (digits.Length >= 2 && (digits[0] - '0') * 10 + (digits[1] - '0') > 23
            || digits.Length >= 3 && digits[2] > '5'
            || digits.Length >= 4 && (digits[2] - '0') * 10 + (digits[3] - '0') > 59)
        {
            Revert(box);
            return;
        }

        var formatted = digits.Length < 2
            ? new string(digits)
            : new string(digits[..2]) + ":" + new string(digits[2..]);
        if (formatted == box.Text)
            return;
        var digitsBeforeCaret = box.Text.Take(box.CaretIndex).Count(char.IsDigit);
        box.Text = formatted;
        box.CaretIndex = IndexAfterDigits(formatted, digitsBeforeCaret);
        box.Tag = formatted;
    }

    private static void Revert(TextBox box)
    {
        var previous = box.Tag as string ?? string.Empty;
        if (box.Text == previous)
            return;
        box.Text = previous;
        box.CaretIndex = previous.Length;
    }

    private static int IndexAfterDigits(string text, int count)
    {
        var seen = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i]))
                continue;
            seen++;
            if (seen == count)
                return i + 1;
        }
        return text.Length;
    }
}
