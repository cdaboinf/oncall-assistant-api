using OnCallHelperApi.Application.DTOs;

namespace OnCallHelperApi.Application.Services;

public interface IOnCallAssistantService
{
    Task<OnCallAssistantResponse> AnalyzeIncidentAsync(string description);
}