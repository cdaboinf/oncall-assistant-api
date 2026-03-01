namespace OnCallHelperApi.Application.DTOs;

public class OnCallAssistantResponse
{
    public string Summary { get; set; }
    public string LikelyRootCause { get; set; }
    public List<string> ImmediateActions { get; set; }
    public List<string> LongTermFixes { get; set; }
    public string EscalationRecommendation { get; set; }
    public string SlackMessageDraft { get; set; }
    public string StatusPageDraft { get; set; }
    public double ConfidenceScore { get; set; }
}