using MongoDB.Driver;
using OnCallHelperApi.Domain;

namespace OnCallHelperApi.Infrastructure.Mongo.Migrations;

public class AddEmbeddingVersionField : IMongoMigration
{
    public string Name => "AddEmbeddingVersionField";

    public async Task ApplyAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<Incident>("incidents");

        await collection.UpdateManyAsync(
            Builders<Incident>.Filter.Empty,
            Builders<Incident>.Update.Set("EmbeddingModelVersion", "v1"));
    }
}