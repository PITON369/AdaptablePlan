using System;

namespace AdaptablePlan.UI.ViewModels;

public static class ViewModelLocator
{
    private static readonly Lazy<MainWindowViewModel> _mainWindow =
        new(() => new MainWindowViewModel());

    public static MainWindowViewModel MainWindow => _mainWindow.Value;
}
