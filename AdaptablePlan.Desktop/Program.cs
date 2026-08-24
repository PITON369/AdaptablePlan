using Avalonia;
using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Settings;
using AdaptablePlan.UI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace AdaptablePlan.Desktop;

class Program
{
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var mongoSettings = new MongoDbSettings
        {
            ConnectionString = configuration["MongoDb:ConnectionString"] ?? string.Empty,
            DatabaseName = configuration["MongoDb:DatabaseName"] ?? string.Empty,
        };

        services.AddSingleton(mongoSettings);
        services.AddSingleton<IAdaptablePlanDbContext, AdaptablePlanDbContext>();

        Services = services.BuildServiceProvider();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
