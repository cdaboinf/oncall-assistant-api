using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;

namespace OnCallHelperMcp;

/// <summary>
/// Thin typed client over the existing OnCallHelperApi REST endpoints. It only calls
/// endpoints that already exist, so the API and the UI are unaffected by this server.
/// </summary>
public class OnCallApiClient(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    // `brief` is ignored by API versions that predate it, so this is safe against any deployed build.
    public Task<JsonElement> AnalyzeAsync(string description, bool brief, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "api/oncall/analyze", new { description, brief }, ct);

    public Task<JsonElement> FindSimilarAsync(string description, int top, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "api/incidents/similar", new { description, top }, ct);

    public Task<JsonElement> ExtractDraftAsync(string conversation, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "api/incidents/extract", new { conversation }, ct);

    public Task<JsonElement> CreateAsync(object body, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, "api/incidents", body, ct);

    public Task<JsonElement> UpdateAsync(string id, object body, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, $"api/incidents/{Uri.EscapeDataString(id)}", body, ct);

    public Task<JsonElement> SearchAsync(IDictionary<string, string?> query, CancellationToken ct)
    {
        var qs = string.Join("&", query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));
        return SendAsync(HttpMethod.Get, "api/incidents" + (qs.Length > 0 ? "?" + qs : ""), null, ct);
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new McpException($"Could not reach the OnCall Helper API at {http.BaseAddress}: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new McpException("The OnCall Helper API timed out.");
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new McpException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "The API rejected the credentials. Set ONCALL_API_TOKEN to a valid bearer token, or disable Auth:Enabled on a local API.",
                    HttpStatusCode.NotFound => "Not found.",
                    _ => $"API returned {(int)response.StatusCode}: {Truncate(text)}"
                });
            }

            return string.IsNullOrWhiteSpace(text)
                ? JsonDocument.Parse("{}").RootElement.Clone()
                : JsonDocument.Parse(text).RootElement.Clone();
        }
    }

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500] + "…";
}
