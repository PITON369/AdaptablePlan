using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AdaptablePlan.UI.ViewModels;

namespace AdaptablePlan.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = Models.AppSettings.Load();
            RequestedThemeVariant = settings.ToThemeVariant();
            Localization.Strings.SetLanguage(settings.ToLanguageCode());
            desktop.MainWindow = new MainWindow
            {
                DataContext = ViewModelLocator.MainWindow,
                FontSize = settings.FontSize,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}