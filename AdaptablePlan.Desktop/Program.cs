using AdaptablePlan.Core.Data;
using AdaptablePlan.Core.Settings;
using AdaptablePlan.UI;
using AdaptablePlan.UI.ViewModels;
using Avalonia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace AdaptablePlan.Desktop;

class Program
{
    public static IServiceProvider Services { get; private set; } = default!;

    /// <summary>
    /// Set to <c>DbType.MongoDb</c> to switch to MongoDB.
    /// Set to <c>DbType.Sqlite</c> to use local SQLite file.
    /// </summary>
    private const DbType DatabaseProvider = DbType.Sqlite;

    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        // --- DB settings ---
        var dbType = Enum.TryParse<DbType>(configuration["DbType"], ignoreCase: true, out var parsed)
            ? parsed
            : DatabaseProvider;

        var mongoConnectionString = configuration["MongoDb:ConnectionString"] ?? string.Empty;
        var mongoDatabaseName = configuration["MongoDb:DatabaseName"] ?? string.Empty;

        services.AddSingleton(new MongoDbSettings
        {
            ConnectionString = mongoConnectionString,
            DatabaseName = mongoDatabaseName,
        });

        services.AddSingleton(typeof(DbType), dbType);

        switch (dbType)
        {
            case DbType.MongoDb:
                services.AddSingleton<IAdaptablePlanDb, MongoAdaptablePlanDb>();
                break;

            case DbType.Sqlite:
            default:
                var sqlitePath = configuration["Sqlite:DatabasePath"]
                    ?? Path.Combine(AppContext.BaseDirectory, "adaptable_plan.db");
                services.AddSingleton<IAdaptablePlanDb>(sp => new SQLiteAdaptablePlanDb(sqlitePath));
                break;
        }

        services.AddSingleton<IAdaptablePlanDbContext, AdaptablePlanDbContext>();

        // --- UI ---
        services.AddSingleton<MainWindowViewModel>();

        Services = services.BuildServiceProvider();

        // Initialize DB (creates SQLite file/tables; no-op for MongoDB)
        var db = Services.GetRequiredService<IAdaptablePlanDb>();
        db.EnsureCreatedAsync().GetAwaiter().GetResult();

        // [TEST] Comment out to simulate "no DB" fallback
        ViewModelLocator.Initialize(Services);

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
