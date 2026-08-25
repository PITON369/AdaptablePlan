using System;
using Microsoft.Extensions.DependencyInjection;

namespace AdaptablePlan.UI.ViewModels;

public static class ViewModelLocator
{
    private static IServiceProvider? _services;
    private static MainWindowViewModel? _cached;

    /// <summary>Call once from Program.Main after building the DI container.</summary>
    public static void Initialize(IServiceProvider services)
    {
        _services = services;
        _cached = null; // reset cache
    }

    public static MainWindowViewModel MainWindow => _cached
        ??= (_services?.GetRequiredService<MainWindowViewModel>()
            ?? new MainWindowViewModel());
}
