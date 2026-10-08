using System.Reflection;
using AdaptablePlan.UI.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AdaptablePlan.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var informationalVersion = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?
            .Split('+')[0];
        if (!string.IsNullOrEmpty(informationalVersion))
            Title = $"AdaptablePlan v{informationalVersion}";
    }

    private void OnCellButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ScheduleItem item } && DataContext is MainWindowViewModel vm)
            vm.SelectedItem = item;
    }
}
