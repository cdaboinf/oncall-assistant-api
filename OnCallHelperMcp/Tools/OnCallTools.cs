using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace OnCallHelperMcp.Tools;

[McpServerToolType]
public class OnCallTools(OnCallApiClient api)
{
    private static string Render(JsonElement e) => JsonSerializer.Serialize(e, OnCallApiClient.Json);

    [McpServerTool(Name = "triage_incident", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Run on-call triage: finds similar past incidents and returns short AI guidance (summary, likely root cause,
        up to 5 immediate actions, escalation advice, confidence score) plus the past incidents it was based on.
        Call this first for any live problem, and call it again as new facts emerge in the conversation (fold
        earlier findings into `description`). Slack/status-page drafts are left out unless `includeDrafts` is true.
        Relay the result briefly; if the confidence is low or no past incident matches, say so rather than padding.
        """)]
    public async Task<string> TriageIncident(
        [Description("What is happening: symptoms, exact error messages, when it started, recent deploys/changes, and what has already been tried.")] string description,
        [Description("Affected service, if known.")] string? serviceName = null,
        [Description("Environment, e.g. prod, staging, if known.")] string? environment = null,
        [Description("Severity, e.g. sev1/sev2/sev3 or low/medium/high/critical, if known.")] string? severity = null,
        [Description("Set true only when the user asks for the Slack update and status-page drafts, and the long-form guidance.")] bool includeDrafts = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new McpException("description is required.");

        var context = new List<string>();
        if (!string.IsNullOrWhiteSpace(serviceName)) context.Add($"Service: {serviceName}");
        if (!string.IsNullOrWhiteSpace(environment)) context.Add($"Environment: {environment}");
        if (!string.IsNullOrWhiteSpace(severity)) context.Add($"Severity: {severity}");
        var full = context.Count == 0 ? description : string.Join("\n", context) + "\n\n" + description;

        var result = await api.AnalyzeAsync(full, brief: !includeDrafts, ct);
        return includeDrafts ? Render(result) : Render(Compact(result));
    }

    // Keeps the chat context small: drops the drafts (also for API builds that don't know `brief`)
    // and shortens the long descriptions of the past incidents, keeping their resolutions intact.
    private static JsonElement Compact(JsonElement triage)
    {
        var node = JsonNode.Parse(triage.GetRawText());
        if (node?["analysis"] is JsonObject analysis)
        {
            analysis.Remove("slackMessageDraft");
            analysis.Remove("statusPageDraft");
        }

        if (node?["similarIncidents"] is JsonArray incidents)
        {
            foreach (var incident in incidents.OfType<JsonObject>())
            {
                if (incident["description"]?.GetValue<string>() is { Length: > 300 } d)
                    incident["description"] = d[..300] + "…";
            }
        }

        return JsonSerializer.SerializeToElement(node);
    }

    [McpServerTool(Name = "find_similar_incidents", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Semantic (vector) search over past incidents. Use for 'have we seen this before?' or to re-check matches after the situation changes. Cheaper than triage_incident: no AI guidance generated. Each result has a similarity score.")]
    public async Task<string> FindSimilarIncidents(
        [Description("Natural-language description of the problem to match against history. Give this (or `query`).")] string? description = null,
        [Description("Same as `description`; accepted so that either name works.")] string? query = null,
        [Description("Number of results, 1-20.")] int top = 5,
        CancellationToken ct = default)
    {
        // Both names are optional in the schema on purpose: a client that validates arguments before sending
        // them rejects a call with the "wrong" name, and the model keeps mixing up description and query.
        var text = !string.IsNullOrWhiteSpace(description) ? description : query;
        if (string.IsNullOrWhiteSpace(text)) throw new McpException("description is required.");
        return Render(await api.FindSimilarAsync(text, Math.Clamp(top, 1, 20), ct));
    }

    [McpServerTool(Name = "search_incidents", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Keyword/filter search over incident history. All filters optional; with none, returns the most recent incidents. The text query is a plain substring match on title, description and service name, so \"timeout\" does not match \"timed out\" and an empty keywordMatches list NEVER means nothing similar exists. When you give a text query, the response also carries semanticMatches (closest incidents by meaning, with relative scores): read both. For any 'have we seen this / known issue' question also call find_similar_incidents with a plain-words description of the problem.")]
    public async Task<string> SearchIncidents(
        [Description("Free-text term matched against title, description and service name.")] string? query = null,
        [Description("Exact service name.")] string? serviceName = null,
        [Description("Severity value as stored on incidents.")] string? severity = null,
        [Description("Environment value as stored on incidents.")] string? environment = null,
        [Description("Only incidents created on/after this ISO-8601 date, e.g. 2026-09-01.")] string? from = null,
        [Description("Only incidents created on/before this ISO-8601 date.")] string? to = null,
        [Description("Max results, 1-100.")] int limit = 10,
        CancellationToken ct = default)
    {
        var result = await api.SearchAsync(new Dictionary<string, string?>
        {
            ["q"] = query,
            ["serviceName"] = serviceName,
            ["severity"] = severity,
            ["environment"] = environment,
            ["from"] = from,
            ["to"] = to,
            ["limit"] = Math.Clamp(limit, 1, 100).ToString()
        }, ct);
        if (string.IsNullOrWhiteSpace(query)) return Render(result);

        // A text query is also searched by meaning, so an empty keyword list is never mistaken for "no history".
        // If the semantic part fails, still return the keyword results and say so.
        JsonElement? semantic = null;
        string note = "keywordMatches is an exact-text match only. semanticMatches are the closest incidents by meaning (scores are relative, about 0.5 to 1): judge by the gap between the best and the rest, and by reading them.";
        try { semantic = await api.FindSimilarAsync(query, 5, ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            note = "The semantic search failed (" + e.Message + "), so only keyword matches are shown. Do not conclude that nothing similar exists; try find_similar_incidents.";
        }

        return Render(JsonSerializer.SerializeToElement(new { keywordMatches = result, semanticMatches = semantic, note }));
    }

    [McpServerTool(Name = "extract_incident_draft", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Turn a pasted chat/Slack conversation into a structured incident draft (title, description, service, environment, severity, resolution). Does NOT save anything: show the draft to the user, let them correct it, then call create_incident.")]
    public async Task<string> ExtractIncidentDraft(
        [Description("Raw conversation text about the incident.")] string conversation,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversation)) throw new McpException("conversation is required.");
        return Render(await api.ExtractDraftAsync(conversation, ct));
    }

    [McpServerTool(Name = "create_incident", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("WRITES a new incident to the shared history and generates its embedding. Only call after the user has approved the exact content. Include the resolution once the incident is resolved: that is what makes it useful for future triage.")]
    public async Task<string> CreateIncident(
        [Description("Short title.")] string title,
        [Description("What happened: symptoms and impact.")] string description,
        [Description("Affected service.")] string serviceName,
        [Description("Environment, e.g. prod.")] string environment,
        [Description("Severity.")] string severity,
        [Description("Root cause, if known.")] string? rootCause = null,
        [Description("Resolution summary, if resolved.")] string? resolutionSummary = null,
        [Description("Ordered steps that fixed it.")] string[]? stepsTaken = null,
        [Description("Who resolved it.")] string? resolvedBy = null,
        CancellationToken ct = default)
    {
        var body = BuildBody(title, description, serviceName, environment, severity, rootCause, resolutionSummary, stepsTaken, resolvedBy);
        return Render(await api.CreateAsync(body, ct));
    }

    [McpServerTool(Name = "update_incident", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("OVERWRITES an existing incident (the API replaces the whole record and recomputes the embedding). Omitted fields keep their current values. Typical use: add the resolution after an incident is resolved. Only call after the user has approved the exact change.")]
    public async Task<string> UpdateIncident(
        [Description("Incident id (from search/triage results).")] string id,
        string? title = null,
        string? description = null,
        string? serviceName = null,
        string? environment = null,
        string? severity = null,
        string? rootCause = null,
        string? resolutionSummary = null,
        string[]? stepsTaken = null,
        string? resolvedBy = null,
        CancellationToken ct = default)
    {
        // The API has no GET-by-id and PUT replaces everything, so read the current record first
        // and merge, otherwise a partial update would blank the other fields.
        var all = await api.SearchAsync(new Dictionary<string, string?> { ["limit"] = "1000" }, ct);
        var current = all.ValueKind == JsonValueKind.Array
            ? all.EnumerateArray().FirstOrDefault(i => i.TryGetProperty("id", out var v) && v.GetString() == id)
            : default;
        if (current.ValueKind != JsonValueKind.Object)
            throw new McpException($"Incident '{id}' was not found among the 1000 most recent incidents.");

        string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        JsonElement res = default;
        var hasRes = current.TryGetProperty("resolution", out res) && res.ValueKind == JsonValueKind.Object;
        string? Cur(string name) => hasRes ? Str(res, name) : null;
        var curSteps = hasRes && res.TryGetProperty("stepsTaken", out var st) && st.ValueKind == JsonValueKind.Array
            ? st.EnumerateArray().Select(s => s.GetString() ?? "").ToArray()
            : null;

        var body = BuildBody(
            title ?? Str(current, "title") ?? "",
            description ?? Str(current, "description") ?? "",
            serviceName ?? Str(current, "serviceName") ?? "",
            environment ?? Str(current, "environment") ?? "",
            severity ?? Str(current, "severity") ?? "",
            rootCause ?? Cur("rootCause"),
            resolutionSummary ?? Cur("summary"),
            stepsTaken ?? curSteps,
            resolvedBy ?? Cur("resolvedBy"));

        return Render(await api.UpdateAsync(id, body, ct));
    }

    private static object BuildBody(string title, string description, string serviceName, string environment,
        string severity, string? rootCause, string? summary, string[]? steps, string? resolvedBy)
    {
        // Always send a complete resolution object, like the UI does. The API's DTO treats all four
        // fields as required, and older API builds fail with a 500 when the object is missing.
        return new
        {
            title, description, serviceName, environment, severity,
            resolution = new
            {
                rootCause = rootCause ?? "",
                summary = summary ?? "",
                stepsTaken = steps?.ToList() ?? [],
                resolvedBy = resolvedBy ?? ""
            }
        };
    }
}
