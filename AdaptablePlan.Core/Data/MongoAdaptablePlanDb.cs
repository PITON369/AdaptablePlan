using AdaptablePlan.Core.Models;
using AdaptablePlan.Core.Settings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
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
    private readonly Lazy<IRepository<CustomPage>> _customPages;
    private readonly Lazy<IRepository<DayOffset>> _dayOffsets;

    public MongoAdaptablePlanDb(MongoDbSettings settings)
    {
        EnsureConventionsRegistered();

        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);

        _taskTemplates = new(() => new MongoRepository<TaskTemplate>(_database.GetCollection<TaskTemplate>("TaskTemplates")));
        _scheduleEntries = new(() => new MongoRepository<ScheduleEntry>(_database.GetCollection<ScheduleEntry>("ScheduleEntries")));
        _customPages = new(() => new MongoRepository<CustomPage>(_database.GetCollection<CustomPage>("CustomPages")));
        _dayOffsets = new(() => new MongoRepository<DayOffset>(_database.GetCollection<DayOffset>("DayOffsets")));
    }

    public IRepository<TaskTemplate> TaskTemplates => _taskTemplates.Value;
    public IRepository<ScheduleEntry> ScheduleEntries => _scheduleEntries.Value;
    public IRepository<CustomPage> CustomPages => _customPages.Value;
    public IRepository<DayOffset> DayOffsets => _dayOffsets.Value;

    public Task EnsureCreatedAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _database.GetCollection<TaskTemplate>("TaskTemplates")
            .DeleteManyAsync(FilterDefinition<TaskTemplate>.Empty, ct);
        await _database.GetCollection<ScheduleEntry>("ScheduleEntries")
            .DeleteManyAsync(FilterDefinition<ScheduleEntry>.Empty, ct);
        await _database.GetCollection<CustomPage>("CustomPages")
            .DeleteManyAsync(FilterDefinition<CustomPage>.Empty, ct);
        await _database.GetCollection<DayOffset>("DayOffsets")
            .DeleteManyAsync(FilterDefinition<DayOffset>.Empty, ct);
    }

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

            // Драйвер 3.x по умолчанию десериализует Guid с GuidRepresentation.Unspecified,
            // что бросает исключение при чтении. Явно задаём Standard (BsonBinaryData subtype 04).
            BsonSerializer.RegisterSerializer(typeof(Guid), new GuidSerializer(GuidRepresentation.Standard));

            _conventionsRegistered = true;
        }
    }
}
