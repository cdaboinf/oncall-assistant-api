using MongoDB.Bson;
using MongoDB.Driver;
using OnCallHelperApi.Application.DTOs.Incident;
using OnCallHelperApi.Domain;
using OnCallHelperApi.Infrastructure.Mongo;

namespace OnCallHelperApi.Infrastructure.Repositories;

public class IncidentRepository(MongoContext context) : IIncidentRepository
{
    private readonly IMongoCollection<Incident> _collection = context.Database.GetCollection<Incident>("incidents");

    public async Task CreateAsync(Incident incident)
    {
        await _collection.InsertOneAsync(incident);
    }
    
    public async Task UpdateAsync(Incident incident)
    {
        await _collection.ReplaceOneAsync(x => x.Id == incident.Id, incident);
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
        var pipeline = new[]
        {
            new BsonDocument("$vectorSearch", new BsonDocument
            {
                { "index", "vector_index" },
                { "path", "Embedding" }, // Embedding
                { "queryVector", new BsonArray(queryVector) },
                { "numCandidates", 50 },
                { "limit", top }
            }),
            new BsonDocument("$addFields", new BsonDocument
            {
                { "score", new BsonDocument("$meta", "vectorSearchScore") }
            })
        };
        
        // Execute the aggregation
        var ragResult = await _collection
            .Aggregate<Incident>(pipeline)
            .ToListAsync();
        
        return ragResult; 
    }
}