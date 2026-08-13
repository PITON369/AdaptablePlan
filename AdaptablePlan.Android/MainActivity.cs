// TODO: Настроить тему, ориентацию, разрешения в AndroidManifest.xml
using Android.App;
using Android.Content.PM;

namespace AdaptablePlan.Android;

[Activity(
    Theme = "@style/MAUI.DefaultTheme",
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : MauiAppCompatActivity
{
}
