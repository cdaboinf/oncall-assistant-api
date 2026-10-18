using Amazon.Lambda.AspNetCoreServer.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

using OnCallHelperMcp.Tools;

namespace OnCallHelperMcp.Hosting;

/// <summary>
/// Hosted mode. Every request needs an Auth0 access token for this server's audience, AND the
/// token's subject must be on an explicit allowlist (fail closed: empty list = nobody gets in).
/// </summary>
public static class HttpHost
{
    public const string OwnerPolicy = "Owner";

    public static async Task RunAsync(string[] args)
    {
        await SsmSecrets.LoadIntoEnvironmentAsync(
            ["ONCALL_AUTH0_CLIENT_ID", "ONCALL_AUTH0_CLIENT_SECRET", "MCP_ALLOWED_USER_IDS"]);

        // Required, no defaults: a hosted server must be told exactly who it trusts.
        var publicUrl = Required("MCP_PUBLIC_URL").TrimEnd('/');      // = the Auth0 API identifier
        var authority = Required("MCP_AUTHORITY").TrimEnd('/') + "/"; // Auth0 tenant (issuer)
        var allowed = (Environment.GetEnvironmentVariable("MCP_ALLOWED_USER_IDS") ?? "")
            .Split([',', ';', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        // Only for local tests against a fake issuer on http://localhost. Never set this in Lambda.
        var insecureAuthority = Environment.GetEnvironmentVariable("MCP_ALLOW_INSECURE_AUTHORITY") == "true";

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
        builder.Services.AddOnCallApi();

        builder.Services
            .AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                o.Authority = authority;
                o.MapInboundClaims = false; // keep the raw "sub" claim name
                o.RequireHttpsMetadata = !insecureAuthority;
                o.TokenValidationParameters.ValidIssuer = authority;
                o.TokenValidationParameters.ValidAudience = publicUrl;
                o.TokenValidationParameters.ValidateIssuer = true;
                o.TokenValidationParameters.ValidateAudience = true;
                o.TokenValidationParameters.ValidateLifetime = true;
                // The default tolerance is 5 minutes, which keeps accepting tokens that already expired.
                o.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            })
            .AddMcp(o =>
            {
                o.ResourceMetadata = new ProtectedResourceMetadata
                {
                    Resource = publicUrl,
                    AuthorizationServers = { authority },
                    ScopesSupported = ["openid", "offline_access"]
                };
            });

        builder.Services.AddAuthorization(o => o.AddPolicy(OwnerPolicy, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => ctx.User.FindFirst("sub")?.Value is { } sub && allowed.Contains(sub))));

        builder.Services
            .AddMcpServer(ApiRegistration.ServerOptions)
            .WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless)
            .WithLenientTools<OnCallTools>()
            .WithPromptsFromAssembly();

        var app = builder.Build();
        if (allowed.Count == 0)
            app.Logger.LogWarning("MCP_ALLOWED_USER_IDS is empty: every request will be rejected.");

        // Behind CloudFront the Host header is the API Gateway hostname, and the 401 challenge builds
        // its metadata URL from the request. This server has exactly one public address, so use it.
        var publicUri = new Uri(publicUrl);
        app.Use((ctx, next) =>
        {
            ctx.Request.Scheme = publicUri.Scheme;
            ctx.Request.Host = publicUri.IsDefaultPort
                ? new HostString(publicUri.Host)
                : new HostString(publicUri.Host, publicUri.Port);
            return next();
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMcp().RequireAuthorization(OwnerPolicy);

        await app.RunAsync();
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v && !string.IsNullOrWhiteSpace(v)
            ? v.Trim()
            : throw new InvalidOperationException($"Hosted mode needs the environment variable {name}.");
}
