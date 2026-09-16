using OnCallHelperApi.Application.DTOs;
using OnCallHelperApi.Application.DTOs.Incident;

namespace OnCallHelperApi.Application.Services;

public interface IOpenAiService
{
    Task<OnCallAssistantResponse> GenerateStructuredResponseAsync(string prompt);

    /// <summary>
    /// Extracts a structured incident draft from free-form text such as a
    /// pasted Slack conversation. The result is a draft for the user to review.
    /// </summary>
    Task<CreateIncidentRequest> ExtractIncidentFromConversationAsync(string conversation);
}