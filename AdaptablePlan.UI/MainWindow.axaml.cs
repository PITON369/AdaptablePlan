using System.ComponentModel;
using System.Reflection;
using AdaptablePlan.UI.ViewModels;
using AdaptablePlan.UI.Views;
using Avalonia.Controls;

namespace AdaptablePlan.UI;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();

        var informationalVersion = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?
            .Split('+')[0];
        if (!string.IsNullOrEmpty(informationalVersion))
            Title = $"AdaptablePlan v{informationalVersion}";

        DataContextChanged += (_, _) => HookViewModel();
        HookViewModel();
    }

    private void HookViewModel()
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as MainWindowViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            UpdatePage();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.IsNewTaskOpen)
            or nameof(MainWindowViewModel.IsSettingsOpen))
            UpdatePage();
    }

    // Одна активная вьюха: форма задачи > настройки > главная.
    private void UpdatePage()
    {
        if (_vm is null)
            return;

        PageHost.Content = _vm.IsNewTaskOpen
            ? (object)new NewTaskEntryView()
            : _vm.IsSettingsOpen
                ? new SettingsView()
                : new MainView();
    }
}
