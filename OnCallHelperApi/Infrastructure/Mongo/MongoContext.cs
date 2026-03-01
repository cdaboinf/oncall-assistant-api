using MongoDB.Driver;
using OnCallHelperApi.Domain;

namespace OnCallHelperApi.Infrastructure.Mongo;

public class MongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(IConfiguration config)
    {
        var client = new MongoClient(config["Mongo:ConnectionString"]);
        _database = client.GetDatabase(config["Mongo:Database"]);
    }

    public IMongoCollection<Incident> Incidents =>
        _database.GetCollection<Incident>("incidents");

    public IMongoCollection<MigrationRecord> Migrations =>
        _database.GetCollection<MigrationRecord>("schema_migrations");

    public IMongoDatabase Database => _database;
}