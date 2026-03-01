using MongoDB.Bson.Serialization.Attributes;
using OnCallHelperApi.Domain;

namespace OnCallHelperApi.Application.DTOs.Incident;

public class IncidentResponse
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string ServiceName { get; set; }
    public string Environment { get; set; }
    public string Severity { get; set; }
    public string Description { get; set; }
    public double? Score { get; set; }
    public DateTime CreatedAt { get; set; }
    public IncidentResolution? Resolution { get; set; } 
}