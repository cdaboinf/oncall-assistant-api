namespace OnCallHelperApi.Domain;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class Incident
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    public string Title { get; set; }

    public IncidentMetadata Metadata { get; set; }
    
    public IncidentResolution? Resolution { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public float[]? Embedding { get; set; }
    public int EmbeddingVersion { get; set; } = 1;
    
    [BsonElement("score")]
    [BsonIgnoreIfNull]
    public double? Score { get; set; }
}