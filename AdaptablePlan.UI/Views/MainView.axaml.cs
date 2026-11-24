using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using System.ComponentModel;

namespace AdaptablePlan.UI.Views;

// Оболочка: переключатель режимов сверху, в ScheduleHost подставляется
// вьюха активного режима (тот же паттерн, что PageHost в MainWindow).
public partial class MainView : UserControl
{
    private MainWindowViewModel? _vm;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = DataContext as MainWindowViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                UpdateScheduleHost();
            }
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.Mode))
            UpdateScheduleHost();
    }

    private void UpdateScheduleHost()
    {
        if (_vm == null)
            return;
        ScheduleHost.Content = _vm.Mode switch
        {
            ScheduleMode.Week => new WeekView(),
            ScheduleMode.Month => new PlanView(),
            ScheduleMode.Year => new PlanView(),
            ScheduleMode.Custom => new CustomPageView(),
            _ => new DayView(),
        };
    }

    private void OnDigitsTextInput(object? sender, TextInputEventArgs e) => DigitsBox.OnTextInput(sender, e);

    private void OnDigitsTextChanged(object? sender, TextChangedEventArgs e) => DigitsBox.OnTextChanged(sender, e);
}
