using OnCallHelperApi.Application.DTOs;

namespace OnCallHelperApi.Application.Services;

public interface IOnCallAssistantService
{
    Task<TriageResult> AnalyzeIncidentAsync(string description);
}