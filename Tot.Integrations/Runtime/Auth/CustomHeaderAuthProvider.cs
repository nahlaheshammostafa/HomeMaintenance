namespace Tot.Integrations.Runtime.Auth;

internal sealed class CustomHeaderAuthProvider : IAuthenticationProvider
{
    private readonly IReadOnlyDictionary<string, string> _headers;

    internal CustomHeaderAuthProvider(IReadOnlyDictionary<string, string> headers) => _headers = headers;

    public Task AuthenticateAsync(HttpRequestMessage request, CancellationToken ct)
    {
        foreach (var (name, value) in _headers)
            request.Headers.TryAddWithoutValidation(name, value);
        return Task.CompletedTask;
    }
}
