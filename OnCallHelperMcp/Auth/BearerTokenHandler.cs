using System.Net;
using System.Net.Http.Headers;

namespace OnCallHelperMcp.Auth;

/// <summary>
/// Adds the cached Auth0 token to every API request. If the API still answers 401 (token
/// revoked or expired early), it gets a fresh token and retries the request once.
/// </summary>
public sealed class BearerTokenHandler(Auth0TokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // A request can only be sent once, so buffer the body to be able to resend it.
        if (request.Content is not null) await request.Content.LoadIntoBufferAsync(ct);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(false, ct));
        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        using var retry = Clone(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(true, ct));
        return await base.SendAsync(retry, ct);
    }

    private static HttpRequestMessage Clone(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Content = original.Content };
        foreach (var h in original.Headers) clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        return clone;
    }
}
