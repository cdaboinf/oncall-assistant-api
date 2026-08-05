using OnCallHelperApi.Application.DTOs.Incident;

namespace OnCallHelperApi.Application.DTOs;

/// <summary>
/// Combined triage payload for the UI: the AI guidance plus the past
/// incidents it was based on, so the on-call developer can see the evidence.
/// </summary>
public class TriageResult
{
    public OnCallAssistantResponse Analysis { get; set; } = new();

    public List<IncidentResponse> SimilarIncidents { get; set; } = new();
}
