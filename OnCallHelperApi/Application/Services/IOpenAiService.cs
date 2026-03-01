using OnCallHelperApi.Application.DTOs;

namespace OnCallHelperApi.Application.Services;

public interface IOpenAiService
{
    Task<OnCallAssistantResponse> GenerateStructuredResponseAsync(string prompt);
}