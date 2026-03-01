using System.Text;
using OnCallHelperApi.Application.DTOs;
using OnCallHelperApi.Domain;
using OnCallHelperApi.Infrastructure.Repositories;

namespace OnCallHelperApi.Application.Services;

public class OnCallAssistantService : IOnCallAssistantService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IIncidentRepository _incidentRepository;
    private readonly IOpenAiService _openAiService;

    public OnCallAssistantService(
        IEmbeddingService embeddingService,
        IIncidentRepository incidentRepository,
        IOpenAiService openAiService)
    {
        _embeddingService = embeddingService;
        _incidentRepository = incidentRepository;
        _openAiService = openAiService;
    }

    public async Task<OnCallAssistantResponse> AnalyzeIncidentAsync(string description)
    {
        // 1️⃣ Generate embedding
        var embedding = await _embeddingService.GetEmbeddingAsync(description);

        // 2️⃣ Get similar incidents
        var similarIncidents = await _incidentRepository.FindSimilarAsync(embedding, 3);

        // 3️⃣ Build prompt
        var prompt = BuildPrompt(description, similarIncidents);

        // 4️⃣ Call OpenAI
        var aiResult = await _openAiService.GenerateStructuredResponseAsync(prompt);

        return aiResult;
    }

    private string BuildPrompt(string description, List<Incident> similar)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are assisting a developer who has just been paged while on call.");
        sb.AppendLine("The developer is new to the system and has very limited context.");
        sb.AppendLine("Your goal is to help them quickly understand the problem and take the correct actions.");
        sb.AppendLine();

        sb.AppendLine("New Incident Description:");
        sb.AppendLine(description);
        sb.AppendLine();

        sb.AppendLine("Relevant Past Incidents and Their Resolutions:");
        sb.AppendLine();

        foreach (var incident in similar)
        {
            sb.AppendLine($"Incident Title: {incident.Title}");
            sb.AppendLine($"Incident Description: {incident.Metadata?.Description}");

            sb.AppendLine($"Service: {incident.Metadata?.ServiceName}");
            sb.AppendLine($"Environment: {incident.Metadata?.Environment}");
            sb.AppendLine($"Severity: {incident.Metadata?.Severity}");

            sb.AppendLine($"Root Cause: {incident.Resolution?.RootCause}");
            sb.AppendLine($"Resolution Summary: {incident.Resolution?.Summary}");

            if (incident.Resolution?.StepsTaken != null)
            {
                sb.AppendLine("Resolution Steps:");
                foreach (var step in incident.Resolution.StepsTaken)
                {
                    sb.AppendLine($"- {step}");
                }
            }

            sb.AppendLine($"Resolved By: {incident.Resolution?.ResolvedBy}");
            sb.AppendLine($"Similarity Score: {incident.Score}");
            sb.AppendLine("-----------------------------------");
        }

        sb.AppendLine(@"
            Respond with structured JSON containing:

            Summary:
            A simple explanation of what is likely happening.

            LikelyRootCause:
            The most probable root cause based on similar incidents.

            ImmediateActions:
            A list of step-by-step actions the on-call developer should try immediately.

            LongTermFixes:
            Possible improvements to prevent this issue in the future.

            EscalationRecommendation:
            When and to whom the issue should be escalated.

            SlackMessageDraft:
            A short update message the developer can post to the incident Slack channel.

            StatusPageDraft:
            A message suitable for posting to a customer-facing status page.

            ConfidenceScore:
            A number between 0 and 1 representing how confident the analysis is.

            Important:
            Base your reasoning primarily on the past incidents and their resolutions.
            Return JSON only.
        ");

        return sb.ToString();
    }
}