using MongoDB.Bson;
using MongoDB.Driver;
using OnCallHelperApi.Application.DTOs.Incident;
using OnCallHelperApi.Domain;
using OnCallHelperApi.Infrastructure.Mongo;

namespace OnCallHelperApi.Infrastructure.Repositories;

public class IncidentRepository : IIncidentRepository
{
    private readonly IMongoCollection<Incident> _collection;

    public IncidentRepository(MongoContext context)
    {
        _collection = context.Database.GetCollection<Incident>("incidents");
    }

    public async Task CreateAsync(Incident incident)
    {
        await _collection.InsertOneAsync(incident);
    }

    public async Task<Incident?> GetByIdAsync(string id)
    {
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<List<Incident>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }
    
    public async Task<List<Incident>> FindSimilarAsync(float[] embedding, int top)
    {
        // Convert float[] to double[] to match MongoDB's expected numeric type
        var queryVector = embedding.Select(f => (double)f).ToArray();

        // Build the $vectorSearch pipeline
        /*var pipeline = new[]
        {
            new BsonDocument
            {
                { "$vectorSearch", new BsonDocument
                    {
                        { "index", "vector_index" },       // Make sure this matches your actual index name
                        { "path", "Embedding" },           // Make sure this matches your collection's embedding field
                        { "queryVector", new BsonArray(queryVector) },
                        { "numCandidates", 50 },
                        { "limit", top }
                    }
                }
            }
        };*/
        var pipeline = new[]
        {
            new BsonDocument("$vectorSearch", new BsonDocument
            {
                { "index", "vector_index" },
                { "path", "Embedding" },
                { "queryVector", new BsonArray(embedding) },
                { "numCandidates", 50 },
                { "limit", top }
            }),
            new BsonDocument("$addFields", new BsonDocument
            {
                { "score", new BsonDocument("$meta", "vectorSearchScore") }
            })
            /*new BsonDocument("$project", new BsonDocument
            {
                { "_id", 1 },
                { "title", 1 },
                { "serviceName", 1 },
                { "environment", 1 },
                { "severity", 1 },
                { "resolvedBy", 1 },
                { "description", 1 },
                { "createdAt", 1 },
                { "score", new BsonDocument("$meta", "vectorSearchScore") }
            })*/
        };

        // Execute the aggregation
        return await _collection
            .Aggregate<Incident>(pipeline)
            .ToListAsync();
    }
}