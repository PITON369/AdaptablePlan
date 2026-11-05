using AdaptablePlan.UI.Models;
using Avalonia;
using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

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

    // Двойной клик по строке = кнопка Edit. Обработчик на ListBox, а не на
    // строке: при выделении строки ListBoxItem перехватывает нажатие, и
    // DoubleTapped поднимается не на строке. Ищем строку подъёмом от источника.
    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Двойной клик по галочке - не редактирование (внутри шаблона CheckBox
        // элементы наследуют DataContext строки, поэтому ищем CheckBox отдельным проходом).
        for (var v = e.Source as Visual; v != null; v = v.GetVisualParent())
            if (v is CheckBox)
                return;

        for (var v = e.Source as Visual; v != null; v = v.GetVisualParent())
        {
            if (v.DataContext is ScheduleItem item && DataContext is MainWindowViewModel vm)
            {
                vm.SelectedItem = item;
                vm.EditTaskCommand.Execute(null);
                return;
            }
        }
    }
}
