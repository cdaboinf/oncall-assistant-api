using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OnCallHelperApi.Application.DTOs;
using OnCallHelperApi.Application.DTOs.Incident;
using OpenAI;
using OpenAI.Chat;

namespace OnCallHelperApi.Application.Services;

public class OpenAiService : IOpenAiService
{
    private readonly ChatClient _chatClient;

    public OpenAiService(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"];

        var client = new OpenAIClient(apiKey);

        _chatClient = client.GetChatClient("gpt-4o-mini");
    }

    public async Task<OnCallAssistantResponse> GenerateStructuredResponseAsync(string prompt)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                """
                You assist developers who were paged for production incidents.

                Respond ONLY with valid JSON matching this schema:

                {
                  "summary": "string",
                  "likelyRootCause": "string",
                  "immediateActions": ["string"],
                  "longTermFixes": ["string"],
                  "escalationRecommendation": "string",
                  "slackMessageDraft": "string",
                  "statusPageDraft": "string",
                  "confidenceScore": number
                }

                Do not include explanations or markdown.
                Return JSON only.
                """
            ),

            new UserChatMessage(prompt)
        };

        // Force the model to return a JSON object so parsing is reliable.
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);

        var json = completion.Content.Count > 0 ? completion.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new OnCallAssistantResponse
            {
                Summary = "The assistant returned an empty response. Try again or rephrase the description."
            };
        }

        try
        {
            var result = JsonSerializer.Deserialize<OnCallAssistantResponse>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return result ?? new OnCallAssistantResponse();
        }
        catch (JsonException)
        {
            return new OnCallAssistantResponse
            {
                Summary = "The assistant response could not be parsed as structured JSON.",
                LikelyRootCause = json
            };
        }
    }

    public async Task<CreateIncidentRequest> ExtractIncidentFromConversationAsync(string conversation)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                """
                You extract ONE structured incident record from an on-call chat
                conversation. The input may be in any form: text copied from the
                Slack UI (with "Name  10:23 AM" headers), a Slack JSON export, or
                API output. Read it all and synthesize across the whole thread.

                Ignore chat artifacts and do not include them in the output:
                - user mentions like <@U12345> or @name (use the plain display name)
                - channel refs like <#C123|channel>, links like <https://x|label>
                  (use the label/URL as plain text)
                - timestamps, message IDs, reaction/emoji codes like :fire:, and
                  quoted/threaded reply markers

                Respond ONLY with valid JSON matching this schema:

                {
                  "title": "string - short, searchable title",
                  "description": "string - what happened, symptoms, impact, when it started",
                  "serviceName": "string - the affected service/system, or empty if unclear",
                  "environment": "string - normalized: production | staging | development | test, or empty",
                  "severity": "string - normalized to sev1|sev2|sev3|sev4, or empty",
                  "resolution": {
                    "rootCause": "string - the underlying cause, or empty if not resolved",
                    "summary": "string - how it was resolved, or empty",
                    "stepsTaken": ["string - each concrete action taken, in order"],
                    "resolvedBy": "string - display name of who resolved it, or empty"
                  }
                }

                Normalization:
                - severity: map synonyms -> sev1 (P1, S1, SEV1, critical, highest),
                  sev2 (P2, S2, high), sev3 (P3, medium), sev4 (P4, low, minor).
                  If no severity is stated or implied, leave it empty. Never guess.
                - environment: map prod/prd/live -> production, stage/stg -> staging,
                  dev -> development, qa -> test. Leave empty if unclear.

                Synthesis rules:
                - Combine information from the entire conversation. When statements
                  conflict, prefer the most recent/confirmed one (e.g. the final
                  confirmed root cause and resolution).
                - stepsTaken: list concrete remediation actions actually performed,
                  in chronological order. Exclude speculation and questions.
                - If the conversation covers multiple distinct incidents, extract the
                  primary one (the most discussed / the one that was resolved).

                Hard rules:
                - Base everything ONLY on the conversation. Do not invent facts.
                - Missing field -> empty string (empty array for stepsTaken).
                - Return JSON only, no markdown or commentary.
                """
            ),
            new UserChatMessage(conversation)
        };

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            // Low temperature -> consistent, deterministic extraction.
            Temperature = 0.2f
        };

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);

        var json = completion.Content.Count > 0 ? completion.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CreateIncidentRequest();
        }

        try
        {
            var result = JsonSerializer.Deserialize<CreateIncidentRequest>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result ?? new CreateIncidentRequest();
        }
        catch (JsonException)
        {
            return new CreateIncidentRequest();
        }
    }
}