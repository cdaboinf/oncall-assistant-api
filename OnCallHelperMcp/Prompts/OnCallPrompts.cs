using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace OnCallHelperMcp.Prompts;

[McpServerPromptType]
public class OnCallPrompts
{
    [McpServerPrompt(Name = "oncall_triage"), Description("Start a guided, conversational triage session for a live incident.")]
    public static ChatMessage Triage(
        [Description("Initial description of the problem, if any.")] string? problem = null) =>
        new(ChatRole.User, $"""
            I'm on call and need help with a production incident. Use the OnCall Helper tools.
            Ask me for anything critical that's missing (service, environment, symptoms, recent changes),
            then triage, show me the likely root cause with evidence from similar past incidents, and
            walk through the immediate actions with me step by step as I report back.

            Problem: {problem ?? "(I'll describe it next)"}
            """);

    [McpServerPrompt(Name = "oncall_postmortem_record"), Description("Record a resolved incident from a pasted conversation.")]
    public static ChatMessage Record(
        [Description("The Slack/chat transcript of the incident.")] string conversation) =>
        new(ChatRole.User, $"""
            Extract an incident draft from this conversation with extract_incident_draft, show it to me,
            apply my corrections, and only then save it with create_incident.

            Conversation:
            {conversation}
            """);
}
