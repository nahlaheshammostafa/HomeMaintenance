using System.Net.Http.Headers;

namespace Tot.Integrations.Runtime.Auth;

internal sealed class BearerTokenAuthProvider : IAuthenticationProvider
{
    private readonly string _token;

    internal BearerTokenAuthProvider(string token) => _token = token;

    public Task AuthenticateAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return Task.CompletedTask;
    }
}
