using System.Net.Http.Headers;
using System.Text.Json;

namespace Tot.Integrations.Runtime.Auth;

/// <summary>
/// OAuth 2.0 Client Credentials flow. Fetches a token from <c>tokenUrl</c>, caches it in
/// <see cref="OAuth2TokenCache"/> (shared singleton), and refreshes 60 s before expiry.
/// A single-flight lock in the cache prevents duplicate refresh calls when many requests race.
/// </summary>
internal sealed class OAuth2ClientCredentialsProvider : IAuthenticationProvider, IHandles401Retry
{
    private readonly Guid _connectionId;
    private readonly string _tokenUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string? _scope;
    private readonly OAuth2TokenCache _cache;
    private readonly HttpClient _tokenClient;

    internal OAuth2ClientCredentialsProvider(
        Guid connectionId,
        string tokenUrl,
        string clientId,
        string clientSecret,
        string? scope,
        OAuth2TokenCache cache,
        HttpClient tokenClient)
    {
        _connectionId = connectionId;
        _tokenUrl = tokenUrl;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _scope = scope;
        _cache = cache;
        _tokenClient = tokenClient;
    }

    void IHandles401Retry.InvalidateCache() => _cache.Invalidate(_connectionId);

    public async Task AuthenticateAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _cache.GetOrRefreshAsync(_connectionId, FetchTokenAsync, ct);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<(string Token, DateTimeOffset ExpiresAt)> FetchTokenAsync(CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
        };
        if (_scope is not null)
            fields["scope"] = _scope;

        using var body = new FormUrlEncodedContent(fields);
        using var response = await _tokenClient.PostAsync(_tokenUrl, body, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("OAuth2 response missing access_token.");

        var expiresIn = root.TryGetProperty("expires_in", out var exp)
            ? exp.GetInt32()
            : 3600;

        return (accessToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }
}
