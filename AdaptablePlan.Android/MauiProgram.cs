// TODO: Проверить корректность UseMauiApp<App>() для Avalonia 12
// В Avalonia 11.x используется Avalonia.Android (не MAUI),
// в Avalonia 12 — Avalonia.Maui. Убедиться что API актуально.
using AdaptablePlan.UI;

namespace AdaptablePlan.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<App>();

        return builder.Build();
    }
}
