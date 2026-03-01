using MongoDB.Driver;

namespace OnCallHelperApi.Infrastructure.Mongo;

public interface IMongoMigration
{
    string Name { get; }
    Task ApplyAsync(IMongoDatabase database);
}