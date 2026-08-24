using AdaptablePlan.Core.Models;
using AdaptablePlan.Core.Settings;
using MongoDB.Driver;

namespace AdaptablePlan.Core.Data;

public interface IAdaptablePlanDbContext
{
    IMongoCollection<TaskTemplate> TaskTemplates { get; }
    IMongoCollection<ScheduleEntry> ScheduleEntries { get; }
}

public class AdaptablePlanDbContext : IAdaptablePlanDbContext
{
    private readonly IMongoDatabase _database;

    public AdaptablePlanDbContext(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
    }

    public IMongoCollection<TaskTemplate> TaskTemplates =>
        _database.GetCollection<TaskTemplate>(nameof(TaskTemplates));

    public IMongoCollection<ScheduleEntry> ScheduleEntries =>
        _database.GetCollection<ScheduleEntry>(nameof(ScheduleEntries));
}
