using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;

namespace OnCallHelperMcp.Auth;

/// <summary>
/// Gets an access token from Auth0 with the client-credentials (machine-to-machine) grant and
/// caches it until shortly before it expires, so calls to the API keep working without anyone
/// pasting a token. Configured by environment variables; the secret is never logged.
/// </summary>
public sealed class Auth0TokenProvider(IHttpClientFactory httpFactory)
{
    public const string HttpClientName = "auth0";

    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static readonly string[] RequiredVars =
        ["ONCALL_AUTH0_DOMAIN", "ONCALL_AUTH0_CLIENT_ID", "ONCALL_AUTH0_CLIENT_SECRET"];

    /// <summary>True when at least one Auth0 variable is set (so the user intends to use it).</summary>
    public static bool IsRequested() => RequiredVars.Any(v => Env(v) is not null);

    public async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && IsFresh()) return _token!;

        await _gate.WaitAsync(ct);
        try
        {
            // Another caller may have refreshed while we waited.
            if (!forceRefresh && IsFresh()) return _token!;
            return await RequestTokenAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsFresh() => _token is not null && DateTimeOffset.UtcNow < _expiresAt - RefreshMargin;

    private async Task<string> RequestTokenAsync(CancellationToken ct)
    {
        var missing = RequiredVars.Where(v => Env(v) is null).ToArray();
        if (missing.Length > 0)
            throw new McpException($"Auth0 is partly configured; missing environment variable(s): {string.Join(", ", missing)}.");

        var domain = Env("ONCALL_AUTH0_DOMAIN")!.TrimEnd('/');
        var baseUri = domain.Contains("://") ? domain : "https://" + domain;
        var audience = Env("ONCALL_AUTH0_AUDIENCE") ?? "http://localhost:5172";

        var body = new
        {
            grant_type = "client_credentials",
            client_id = Env("ONCALL_AUTH0_CLIENT_ID"),
            client_secret = Env("ONCALL_AUTH0_CLIENT_SECRET"),
            audience
        };

        HttpResponseMessage response;
        try
        {
            // StringContent (fixed Content-Length) rather than streamed JSON: plain and widely accepted.
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            response = await httpFactory.CreateClient(HttpClientName)
                .PostAsync($"{baseUri}/oauth/token", content, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new McpException($"Could not reach Auth0 at {baseUri}: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // Auth0 error bodies describe the problem (e.g. access_denied) and never echo the secret.
                var detail = await response.Content.ReadAsStringAsync(ct);
                throw new McpException($"Auth0 rejected the token request ({(int)response.StatusCode}): {Truncate(detail)}");
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            if (string.IsNullOrEmpty(token?.AccessToken))
                throw new McpException("Auth0 returned no access_token.");

            _token = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600);
            return _token;
        }
    }

    private static string Truncate(string s) => s.Length <= 300 ? s : s[..300] + "…";

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
