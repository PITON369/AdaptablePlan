using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AdaptablePlan.Core.Data;

internal sealed class SqliteRepository<T> : IRepository<T> where T : notnull
{
    private readonly SQLiteAdaptablePlanDb _db;
    private readonly string _table;
    private readonly PropertyInfo? _idProperty;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
    };

    public SqliteRepository(SQLiteAdaptablePlanDb db, string table)
    {
        _db = db;
        _table = table;
        _idProperty = typeof(T).GetProperty("Id");
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => _db.QueryAllAsync<T>(_table, ct);

    public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        var idStr = id.ToString() ?? throw new ArgumentNullException(nameof(id));
        return await _db.QueryByIdAsync<T>(_table, idStr, ct);
    }

    public async Task InsertAsync(T entity, CancellationToken ct = default)
    {
        var id = GetIdValue(entity);
        var json = JsonSerializer.Serialize(entity, JsonOpts);
        await _db.InsertRowAsync(_table, id, json, ct);
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        var id = GetIdValue(entity);
        var json = JsonSerializer.Serialize(entity, JsonOpts);
        await _db.InsertRowAsync(_table, id, json, ct);
    }

    public async Task DeleteAsync(object id, CancellationToken ct = default)
    {
        var idStr = id.ToString() ?? throw new ArgumentNullException(nameof(id));
        await _db.DeleteRowAsync(_table, idStr, ct);
    }

    public Task EnsureCreatedAsync(CancellationToken ct = default)
        => _db.EnsureCreatedAsync(ct);

    private string GetIdValue(T entity)
    {
        var val = _idProperty?.GetValue(entity)
            ?? throw new InvalidOperationException($"Type {typeof(T).Name} has no Id property or it's null.");
        return val.ToString() ?? throw new InvalidOperationException($"Id of {typeof(T).Name} is null.");
    }
}
