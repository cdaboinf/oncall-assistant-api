using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OnCallHelperApi.Application.DTOs;
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

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages);

        var json = completion.Content[0].Text;

        var result = JsonSerializer.Deserialize<OnCallAssistantResponse>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return result ?? new OnCallAssistantResponse();
    }
}