namespace OnCallHelperApi.Application.DTOs;

public class AnalyzeIncidentRequest
{
    public string Description { get; set; }

    /// <summary>
    /// Optional. When true, returns short, prioritized guidance without the Slack/status-page
    /// drafts. Defaults to false so existing callers (the UI) get the same output as before.
    /// </summary>
    public bool Brief { get; set; }
}