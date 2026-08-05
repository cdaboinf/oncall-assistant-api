using System.Text.RegularExpressions;
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

    public async Task<List<Incident>> SearchAsync(IncidentSearchRequest query)
    {
        var fb = Builders<Incident>.Filter;
        var filters = new List<FilterDefinition<Incident>>();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            // Case-insensitive contains match across title/description/service.
            var regex = new BsonRegularExpression(Regex.Escape(query.Q.Trim()), "i");
            filters.Add(fb.Or(
                fb.Regex(x => x.Title, regex),
                fb.Regex("Metadata.Description", regex),
                fb.Regex("Metadata.ServiceName", regex)));
        }

        if (!string.IsNullOrWhiteSpace(query.ServiceName))
            filters.Add(fb.Eq("Metadata.ServiceName", query.ServiceName.Trim()));

        if (!string.IsNullOrWhiteSpace(query.Severity))
            filters.Add(fb.Eq("Metadata.Severity", query.Severity.Trim()));

        if (!string.IsNullOrWhiteSpace(query.Environment))
            filters.Add(fb.Eq("Metadata.Environment", query.Environment.Trim()));

        if (query.From.HasValue)
            filters.Add(fb.Gte(x => x.CreatedAt, query.From.Value));

        if (query.To.HasValue)
            filters.Add(fb.Lte(x => x.CreatedAt, query.To.Value));

        var filter = filters.Count > 0 ? fb.And(filters) : fb.Empty;
        var limit = query.Limit is > 0 and <= 500 ? query.Limit : 100;

        return await _collection
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Limit(limit)
            .ToListAsync();
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