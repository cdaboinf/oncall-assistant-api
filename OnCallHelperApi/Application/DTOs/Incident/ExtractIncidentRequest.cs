namespace OnCallHelperApi.Application.DTOs.Incident;

/// <summary>
/// Raw text (e.g. a pasted Slack conversation) to extract an incident draft from.
/// </summary>
public class ExtractIncidentRequest
{
    public string Conversation { get; set; } = string.Empty;
}
