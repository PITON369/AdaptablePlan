using AdaptablePlan.Core.Models;
using AdaptablePlan.Core.Settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AdaptablePlan.Core.Data;

internal sealed class MongoAdaptablePlanDb : IAdaptablePlanDb
{
    private static readonly object _lock = new();
    private static bool _conventionsRegistered;

    private readonly IMongoDatabase _database;
    private readonly Lazy<IRepository<TaskTemplate>> _taskTemplates;
    private readonly Lazy<IRepository<ScheduleEntry>> _scheduleEntries;

    public MongoAdaptablePlanDb(MongoDbSettings settings)
    {
        EnsureConventionsRegistered();

        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);

        _taskTemplates = new(() => new MongoRepository<TaskTemplate>(_database.GetCollection<TaskTemplate>("TaskTemplates")));
        _scheduleEntries = new(() => new MongoRepository<ScheduleEntry>(_database.GetCollection<ScheduleEntry>("ScheduleEntries")));
    }

    public IRepository<TaskTemplate> TaskTemplates => _taskTemplates.Value;
    public IRepository<ScheduleEntry> ScheduleEntries => _scheduleEntries.Value;

    public Task EnsureCreatedAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    private static void EnsureConventionsRegistered()
    {
        lock (_lock)
        {
            if (_conventionsRegistered)
                return;

            var pack = new ConventionPack
            {
                new EnumRepresentationConvention(BsonType.Int32),
            };
            ConventionRegistry.Register("app_conventions", pack, _ => true);

            _conventionsRegistered = true;
        }
    }
}
