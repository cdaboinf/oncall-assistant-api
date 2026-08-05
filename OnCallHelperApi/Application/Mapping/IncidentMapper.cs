using OnCallHelperApi.Application.DTOs.Incident;
using OnCallHelperApi.Domain;

namespace OnCallHelperApi.Application.Mapping;

public static class IncidentMapper
{
    public static IncidentResponse ToResponse(Incident incident)
    {
        return new IncidentResponse
        {
            Id = incident.Id,
            Title = incident.Title,
            Description = incident.Metadata?.Description ?? string.Empty,
            ServiceName = incident.Metadata?.ServiceName ?? string.Empty,
            Environment = incident.Metadata?.Environment ?? string.Empty,
            Severity = incident.Metadata?.Severity ?? string.Empty,
            Score = incident.Score,
            CreatedAt = incident.CreatedAt,
            Resolution = incident.Resolution != null
                ? new IncidentResolution
                {
                    RootCause = incident.Resolution.RootCause,
                    Summary = incident.Resolution.Summary,
                    StepsTaken = incident.Resolution.StepsTaken ?? [],
                    ResolvedBy = incident.Resolution.ResolvedBy
                }
                : null
        };
    }
}
