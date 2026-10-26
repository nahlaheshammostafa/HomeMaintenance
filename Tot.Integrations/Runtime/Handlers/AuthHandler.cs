using Tot.Integrations.Runtime.Auth;

namespace Tot.Integrations.Runtime.Handlers;

/// <summary>
/// Applies the connection's <see cref="IAuthenticationProvider"/> to every outbound request.
/// A <see langword="null"/> provider (AuthScheme.None) is a no-op.
/// For OAuth2 providers (<see cref="IHandles401Retry"/>), a single 401 response triggers
/// one token-cache invalidation and one retry with fresh credentials.
/// </summary>
internal sealed class AuthHandler : DelegatingHandler
{
    private readonly IAuthenticationProvider? _auth;

    internal AuthHandler(IAuthenticationProvider? auth) => _auth = auth;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_auth is not null)
            await _auth.AuthenticateAsync(request, ct);

        // Buffer any body so it can be re-sent on a 401 retry
        if (request.Content is not null)
            await request.Content.LoadIntoBufferAsync();

        var response = await base.SendAsync(request, ct);

        // Retry once on 401 for providers that support token refresh (e.g. OAuth2)
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            && _auth is IHandles401Retry refreshable)
        {
            refreshable.InvalidateCache();
            response.Dispose();

            var retry = await CloneRequestAsync(request, ct);
            await _auth.AuthenticateAsync(retry, ct);
            return await base.SendAsync(retry, ct);
        }

        return response;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage original, CancellationToken ct)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var (key, values) in original.Headers)
            clone.Headers.TryAddWithoutValidation(key, values);

        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync(ct);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var (key, values) in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(key, values);
        }

        return clone;
    }
}
