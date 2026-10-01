using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AdaptablePlan.Core.Data;

// Внутри используются синхронные драйверные вызовы (FindSync/InsertOne/...),
// а методы возвращают уже выполненные задачи. Вызовы идут с UI-потока через
// .GetAwaiter().GetResult() — await внутри методов захватил бы контекст UI-потока
// и возник бы дедлок, как это происходило с async-версией.
internal sealed class MongoRepository<T> : IRepository<T> where T : notnull
{
    private readonly IMongoCollection<T> _collection;
    private readonly PropertyInfo? _idProperty;

    public MongoRepository(IMongoCollection<T> collection)
    {
        _collection = collection;
        _idProperty = typeof(T).GetProperty("Id");
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<T>>(
            _collection.FindSync(FilterDefinition<T>.Empty, cancellationToken: ct)
                .ToEnumerable(ct).ToList());

    public Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => Task.FromResult(
            _collection.FindSync(Builders<T>.Filter.Eq("_id", id), cancellationToken: ct)
                .ToEnumerable(ct).SingleOrDefault());

    public Task InsertAsync(T entity, CancellationToken ct = default)
    {
        _collection.InsertOne(entity, null, ct);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        var entityId = _idProperty?.GetValue(entity)
            ?? throw new InvalidOperationException($"Type {typeof(T).Name} has no Id value.");

        _collection.ReplaceOne(
            Builders<T>.Filter.Eq("_id", entityId),
            entity,
            new ReplaceOptions { IsUpsert = false },
            ct);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(object id, CancellationToken ct = default)
    {
        _collection.DeleteOne(Builders<T>.Filter.Eq("_id", id), ct);
        return Task.CompletedTask;
    }

    public Task EnsureCreatedAsync(CancellationToken ct = default)
        => Task.CompletedTask;
}
