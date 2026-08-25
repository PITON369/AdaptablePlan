using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AdaptablePlan.Core.Data;

internal sealed class MongoRepository<T> : IRepository<T> where T : notnull
{
    private readonly IMongoCollection<T> _collection;
    private readonly PropertyInfo? _idProperty;

    public MongoRepository(IMongoCollection<T> collection)
    {
        _collection = collection;
        _idProperty = typeof(T).GetProperty("Id");
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _collection.Find(_ => true).ToListAsync(ct);

    public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => await _collection.Find(Builders<T>.Filter.Eq("id", id)).SingleOrDefaultAsync(ct);

    public async Task InsertAsync(T entity, CancellationToken ct = default)
        => await _collection.InsertOneAsync(entity, null, ct);

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        var entityId = _idProperty?.GetValue(entity);
        if (entityId == null)
            throw new InvalidOperationException($"Type {typeof(T).Name} has no Id value.");

        await _collection.ReplaceOneAsync(
            Builders<T>.Filter.Eq("id", entityId),
            entity,
            new ReplaceOptions { IsUpsert = false },
            ct);
    }

    public async Task DeleteAsync(object id, CancellationToken ct = default)
        => await _collection.DeleteOneAsync(Builders<T>.Filter.Eq("id", id), ct);

    public Task EnsureCreatedAsync(CancellationToken ct = default)
        => Task.CompletedTask;
}
