using System.Net.Http.Headers;
using System.Text;

namespace Tot.Integrations.Runtime.Auth;

internal sealed class BasicAuthProvider : IAuthenticationProvider
{
    private readonly string _encoded;

    internal BasicAuthProvider(string username, string password)
    {
        _encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
    }

    public Task AuthenticateAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _encoded);
        return Task.CompletedTask;
    }
}
