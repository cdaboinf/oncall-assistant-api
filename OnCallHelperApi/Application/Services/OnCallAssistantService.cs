using System.Text;
using OnCallHelperApi.Application.DTOs;
using OnCallHelperApi.Application.Mapping;
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

    public async Task<TriageResult> AnalyzeIncidentAsync(string description, bool brief = false)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return new TriageResult
            {
                Analysis = new OnCallAssistantResponse
                {
                    Summary = "Provide an alert or symptom description to analyze."
                }
            };
        }

        // 1️⃣ Generate embedding
        var embedding = await _embeddingService.GetEmbeddingAsync(description);

        // 2️⃣ Get similar incidents
        var similarIncidents = await _incidentRepository.FindSimilarAsync(embedding, 3);

        // 3️⃣ Build prompt
        var prompt = BuildPrompt(description, similarIncidents, brief);

        // 4️⃣ Call OpenAI
        var aiResult = await _openAiService.GenerateStructuredResponseAsync(prompt);

        // 5️⃣ Return guidance plus the evidence it was based on
        return new TriageResult
        {
            Analysis = aiResult,
            SimilarIncidents = similarIncidents.Select(IncidentMapper.ToResponse).ToList()
        };
    }

    // Used only when the caller asks for brief output (e.g. the MCP server).
    // Same JSON fields as the default prompt, so the response shape never changes.
    private const string BriefInstructions = """
        Respond with structured JSON using the same fields as always, but be brief and practical.
        Write for an engineer in the middle of an incident: no background lessons, no filler.

        summary: at most 2 sentences. What is most likely happening.
        likelyRootCause: 1-2 sentences, naming the specific cause from the past incidents if one clearly applies.
        immediateActions: at most 5 items, most important first, each one a single concrete step (command, check or owner), not a generic best practice.
        longTermFixes: at most 2 items, only if the past incidents point to one; otherwise [].
        escalationRecommendation: 1 sentence: when and who.
        slackMessageDraft: "" (leave empty).
        statusPageDraft: "" (leave empty).
        confidenceScore: 0 to 1.

        Rules:
        - Ground every claim in the past incidents above. Do not invent systems, commands or details.
        - When a past incident clearly matches, build immediateActions from the "Resolution Steps" that
          actually fixed it, adapted to the new incident and ordered for what to try first. Name the
          past incident in the step (e.g. "As in 'Payment service pods crashing': check the DB host env var").
          Only add generic checks if fewer than 3 steps come from past incidents.
        - If past incidents point to different causes, list the cause that best fits the new facts first
          and say which fact would tell the user it is the wrong one.
        - If none of the past incidents clearly matches the new incident, say so plainly in summary,
          give only generic first checks that are safe to run, and keep confidenceScore below 0.4.
        - If key information is missing (for example service or environment), mention in summary
          what you would need to know, instead of guessing.
        Return JSON only.
        """;

    private string BuildPrompt(string description, List<Incident> similar, bool brief)
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

        if (brief)
        {
            sb.AppendLine(BriefInstructions);
            return sb.ToString();
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