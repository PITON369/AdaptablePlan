using AdaptablePlan.Core.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AdaptablePlan.Core.Data;

internal static class SqliteParamExtensions
{
    public static void Add(this DbCommand cmd, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = "@p" + cmd.Parameters.Count;
        param.Value = value is null ? DBNull.Value : value;
        cmd.Parameters.Add(param);
    }
}

internal sealed class SQLiteAdaptablePlanDb : IAdaptablePlanDb, IDisposable
{
    private readonly string _dbPath;
    private DbConnection? _connection;

    private DbConnection Connection
    {
        get
        {
            _connection ??= new SqliteConnection($"Data Source={_dbPath}");
            if (_connection.State != System.Data.ConnectionState.Open)
                _connection.Open();
            return _connection;
        }
    }

    private readonly Lazy<IRepository<TaskTemplate>> _taskTemplates;
    private readonly Lazy<IRepository<ScheduleEntry>> _scheduleEntries;

    public SQLiteAdaptablePlanDb(string dbPath)
    {
        _dbPath = dbPath;
        _taskTemplates = new(() => new SqliteRepository<TaskTemplate>(this, "TaskTemplates"));
        _scheduleEntries = new(() => new SqliteRepository<ScheduleEntry>(this, "ScheduleEntries"));
    }

    public IRepository<TaskTemplate> TaskTemplates => _taskTemplates.Value;
    public IRepository<ScheduleEntry> ScheduleEntries => _scheduleEntries.Value;

    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        var conn = Connection;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS TaskTemplates (
                    Id TEXT PRIMARY KEY,
                    Body TEXT NOT NULL
                )
            """;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS ScheduleEntries (
                    Id TEXT PRIMARY KEY,
                    Body TEXT NOT NULL
                )
            """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    internal async Task<string?> FindIdAsync(string table, string id, CancellationToken ct)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = $"SELECT Id FROM {table} WHERE Id = @p0";
        cmd.Add(id);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? reader.GetString(0) : null;
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var conn = Connection;
        foreach (var table in new[] { "TaskTemplates", "ScheduleEntries" })
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM {table}";
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    internal async Task InsertRowAsync(string table, string id, string body, CancellationToken ct)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = $"INSERT OR REPLACE INTO {table} (Id, Body) VALUES (@p0, @p1)";
        cmd.Add(id);
        cmd.Add(body);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal async Task DeleteRowAsync(string table, string id, CancellationToken ct)
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM {table} WHERE Id = @p0";
        cmd.Add(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal async Task<IReadOnlyList<T>> QueryAllAsync<T>(string table, CancellationToken ct) where T : notnull
    {
        var results = new List<T>();
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = $"SELECT Body FROM {table}";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), JsonOpts)!);
        return results;
    }

    internal async Task<T?> QueryByIdAsync<T>(string table, string id, CancellationToken ct) where T : notnull
    {
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = $"SELECT Body FROM {table} WHERE Id = @p0";
        cmd.Add(id);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return JsonSerializer.Deserialize<T>(reader.GetString(0), JsonOpts);
        return default;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
