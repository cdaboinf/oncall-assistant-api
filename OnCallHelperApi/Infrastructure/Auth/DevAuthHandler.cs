using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OnCallHelperApi.Infrastructure.Auth;

/// <summary>
/// Authentication handler used only when "Auth:Enabled" is false.
/// It authenticates every request as a fixed local developer so that
/// [Authorize] controllers work without a token during local/personal use.
/// Never enable this in a deployed/shared environment.
/// </summary>
public class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevAuth";

    public DevAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "local-dev"),
            new Claim(ClaimTypes.Name, "Local Developer"),
            new Claim("scope", "local")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
