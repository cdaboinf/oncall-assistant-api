using MongoDB.Driver;
using OnCallHelperApi.Domain;

namespace OnCallHelperApi.Infrastructure.Mongo.Migrations;

public class CreateInitialIndexes : IMongoMigration
{
    public string Name => "CreateInitialIndexes";

    public async Task ApplyAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<Incident>("incidents");

        var serviceIndex = Builders<Incident>
            .IndexKeys
            .Ascending(x => x.Metadata.ServiceName);

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<Incident>(serviceIndex));
    }
}