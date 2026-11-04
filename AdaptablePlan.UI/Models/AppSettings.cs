using System;
using System.IO;
using System.Text.Json;
using Avalonia.Styling;

namespace AdaptablePlan.UI.Models;

// Настройки внешнего вида: размер шрифта и тема. Хранятся в
// %APPDATA%\AdaptablePlan\settings.json (не в appsettings.json — тот
// служебный, с паролем БД, и не коммитится).
public class AppSettings
{
    public double FontSize { get; set; } = 12;

    // System = следовать системной теме (RequestedThemeVariant Default).
    public string Theme { get; set; } = "System";

    public ThemeVariant ToThemeVariant() => Theme switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    public static string SettingsPath
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AdaptablePlan", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch
        {
            // битый/недоступный файл — используем значения по умолчанию
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this));
        }
        catch
        {
            // запись не удалась — настройки просто не сохранятся
        }
    }
}
