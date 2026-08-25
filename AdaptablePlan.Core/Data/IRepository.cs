using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AdaptablePlan.Core.Data;

public interface IRepository<T> where T : notnull
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task InsertAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(object id, CancellationToken ct = default);
    Task EnsureCreatedAsync(CancellationToken ct = default);
}
