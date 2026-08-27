using AdaptablePlan.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AdaptablePlan.Core.Data;

/// <summary>
/// Database-agnostic context providing repository access to all entity collections.
/// </summary>
public interface IAdaptablePlanDb
{
    IRepository<TaskTemplate> TaskTemplates { get; }
    IRepository<ScheduleEntry> ScheduleEntries { get; }

    Task EnsureCreatedAsync(CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}
