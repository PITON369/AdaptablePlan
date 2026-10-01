using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdaptablePlan.Core.Models;

namespace AdaptablePlan.Core.Data;

public class SqliteDbSettings
{
    public string DatabasePath { get; init; } = string.Empty;
}

// Дневной бэкап: при первом запуске за день копирует данные активного
// провайдера (Mongo на сервере) в локальный SQLite-файл. Дата последнего
// бэкапа хранится в файле-маркере рядом с базой (<путь>.backup-stamp).
public static class SqliteBackup
{
    public static async Task BackupTodayAsync(IAdaptablePlanDb source, string sqlitePath, CancellationToken ct = default)
    {
        var stampPath = sqlitePath + ".backup-stamp";
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        if (File.Exists(stampPath) && File.ReadAllText(stampPath).Trim() == today)
            return;

        var templates = await source.TaskTemplates.GetAllAsync(ct);
        var entries = await source.ScheduleEntries.GetAllAsync(ct);

        using var target = new SQLiteAdaptablePlanDb(sqlitePath);
        await target.EnsureCreatedAsync(ct);
        await target.ClearAsync(ct);
        foreach (var t in templates)
            await target.TaskTemplates.InsertAsync(t, ct);
        foreach (var e in entries)
            await target.ScheduleEntries.InsertAsync(e, ct);

        File.WriteAllText(stampPath, today);
    }

    // Загрузка из локальной SQLite-копии — fallback, когда сервер БД недоступен.
    // null = файла нет или в нём нет данных.
    public static async Task<(List<TaskTemplate> Templates, List<ScheduleEntry> Entries)?> LoadFromSqliteAsync(
        string sqlitePath, CancellationToken ct = default)
    {
        if (!File.Exists(sqlitePath))
            return null;

        using var db = new SQLiteAdaptablePlanDb(sqlitePath);
        await db.EnsureCreatedAsync(ct);
        var templates = await db.TaskTemplates.GetAllAsync(ct);
        var entries = await db.ScheduleEntries.GetAllAsync(ct);
        if (templates.Count == 0 && entries.Count == 0)
            return null;

        return (templates.ToList(), entries.ToList());
    }

    // Дата последнего бэкапа из файла-маркера, для показа в баннере.
    public static string? ReadLastBackupDate(string sqlitePath)
    {
        var stampPath = sqlitePath + ".backup-stamp";
        return File.Exists(stampPath) ? File.ReadAllText(stampPath).Trim() : null;
    }
}
