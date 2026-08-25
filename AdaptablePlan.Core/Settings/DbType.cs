namespace AdaptablePlan.Core.Settings;

/// <summary>
/// Which database provider to use. Change this flag to switch between MongoDB and SQLite.
/// </summary>
public enum DbType
{
    Sqlite,
    MongoDb
}
