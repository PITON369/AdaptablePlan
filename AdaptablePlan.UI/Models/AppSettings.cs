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

    // System = следовать языку системы; иначе English / Russian.
    public string Language { get; set; } = "System";

    // Скрыть в Day-списке столбцы с базовым (несдвинутым) временем.
    public bool HideUnshiftedTime { get; set; }

    // Граница дня "HH:mm": план-время до неё относится к предыдущему дню.
    public string DayStart { get; set; } = "07:00";

    // Свой звук окончания таски (.wav); пусто - стандартный системный.
    public string SoundFile { get; set; } = string.Empty;

    public ThemeVariant ToThemeVariant() => Theme switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    public string ToLanguageCode() => Language switch
    {
        "English" => "en",
        "Russian" => "ru",
        _ => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en",
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
