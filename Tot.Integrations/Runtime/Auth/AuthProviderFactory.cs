using System.Text.Json;

namespace Tot.Integrations.Runtime.Auth;

/// <summary>
/// Builds an <see cref="IAuthenticationProvider"/> from a connection's
/// <see cref="AuthScheme"/> and the raw secret JSON stored in <see cref="ISecretStore"/>.
/// </summary>
internal sealed class AuthProviderFactory
{
    private readonly OAuth2TokenCache _tokenCache;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthProviderFactory(OAuth2TokenCache tokenCache, IHttpClientFactory httpClientFactory)
    {
        _tokenCache = tokenCache;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Reads the secret for <paramref name="connection"/> and builds the appropriate provider.
    /// Returns <see langword="null"/> for <see cref="AuthScheme.None"/>.
    /// </summary>
    internal async Task<IAuthenticationProvider?> CreateAsync(
        IntegrationConnection connection,
        AuthScheme scheme,
        ISecretStore secretStore,
        CancellationToken ct)
    {
        if (scheme == AuthScheme.None)
            return null;

        var rawSecret = await secretStore.GetAsync(connection.SecretRef, ct);

        var secrets = rawSecret is null
            ? new Dictionary<string, string?>()
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(rawSecret) ?? [];

        return scheme switch
        {
            AuthScheme.ApiKey => new ApiKeyAuthProvider(
                headerOrParamName: Get(secrets, "apiKeyHeader", "X-Api-Key"),
                value: Require(secrets, "apiKey", connection, "apiKey"),
                inQuery: secrets.TryGetValue("apiKeyInQuery", out var q) && q == "true"),

            AuthScheme.Bearer => new BearerTokenAuthProvider(
                Require(secrets, "token", connection, "token")),

            AuthScheme.Basic => new BasicAuthProvider(
                username: Require(secrets, "username", connection, "username"),
                password: Require(secrets, "password", connection, "password")),

            AuthScheme.Hmac => new HmacSigningProvider(
                signingKey: Require(secrets, "signingKey", connection, "signingKey"),
                algorithm: secrets.TryGetValue("hmacAlgorithm", out var alg) && alg == "sha512"
                    ? HmacSigningProvider.HmacAlgorithm.Sha512
                    : HmacSigningProvider.HmacAlgorithm.Sha256,
                timestampHeader: Get(secrets, "timestampHeader", "X-Timestamp"),
                signatureHeader: Get(secrets, "signatureHeader", "X-Signature")),

            AuthScheme.OAuth2ClientCredentials => new OAuth2ClientCredentialsProvider(
                connectionId: connection.Id,
                tokenUrl: Require(secrets, "tokenUrl", connection, "tokenUrl"),
                clientId: Require(secrets, "clientId", connection, "clientId"),
                clientSecret: Require(secrets, "clientSecret", connection, "clientSecret"),
                scope: secrets.TryGetValue("scope", out var s) ? s : null,
                cache: _tokenCache,
                tokenClient: _httpClientFactory.CreateClient("integrations:oauth2")),

            AuthScheme.Custom =>
                throw new InvalidOperationException(
                    $"Custom auth for connection '{connection.Id}' must be registered by the provider " +
                    "via IIntegrationsBuilder.AddCapability — AuthProviderFactory cannot build it."),

            _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, null)
        };
    }

    private static string Require(
        Dictionary<string, string?> secrets,
        string key,
        IntegrationConnection connection,
        string fieldName)
    {
        if (secrets.TryGetValue(key, out var v) && v is not null)
            return v;
        throw new InvalidOperationException(
            $"Secret for connection '{connection.Id}' is missing required field '{fieldName}'.");
    }

    private static string Get(Dictionary<string, string?> secrets, string key, string @default) =>
        secrets.TryGetValue(key, out var v) && v is not null ? v : @default;
}
