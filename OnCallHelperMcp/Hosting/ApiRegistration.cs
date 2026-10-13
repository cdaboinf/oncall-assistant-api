using OnCallHelperMcp.Auth;

namespace OnCallHelperMcp.Hosting;

/// <summary>How the server calls the OnCall Helper API; identical in stdio and HTTP mode.</summary>
public static class ApiRegistration
{
    public static IServiceCollection AddOnCallApi(this IServiceCollection services)
    {
        var baseUrl = Environment.GetEnvironmentVariable("ONCALL_API_BASE_URL") ?? "http://localhost:5172";
        var token = Environment.GetEnvironmentVariable("ONCALL_API_TOKEN");

        // Auth, in priority order:
        //   1. ONCALL_API_TOKEN: a fixed bearer token (manual override; it will expire).
        //   2. ONCALL_AUTH0_DOMAIN / _CLIENT_ID / _CLIENT_SECRET (/_AUDIENCE): machine-to-machine token,
        //      fetched and refreshed automatically. Use this for the deployed API.
        //   3. Neither: no Authorization header (local API with Auth:Enabled=false).
        var useAuth0 = string.IsNullOrWhiteSpace(token) && Auth0TokenProvider.IsRequested();

        services.AddHttpClient(Auth0TokenProvider.HttpClientName, http => http.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<Auth0TokenProvider>();
        services.AddTransient<BearerTokenHandler>();

        var apiClient = services.AddHttpClient<OnCallApiClient>(http =>
        {
            http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            // Triage calls OpenAI + vector search, so allow for slow responses.
            http.Timeout = TimeSpan.FromSeconds(90);
            if (!string.IsNullOrWhiteSpace(token))
                http.DefaultRequestHeaders.Authorization = new("Bearer", token);
        });
        if (useAuth0) apiClient.AddHttpMessageHandler<BearerTokenHandler>();

        return services;
    }

    public static Action<ModelContextProtocol.Server.McpServerOptions> ServerOptions => o =>
    {
        o.ServerInfo = new() { Name = "oncall-helper", Version = "1.0.0" };
        o.ServerInstructions = ServerInstructions.Text;
    };
}
