namespace Tot.Integrations;

/// <summary>Authentication scheme used by a provider connection.</summary>
public enum AuthScheme
{
    /// <summary>No authentication.</summary>
    None,
    /// <summary>API key sent in a request header or query parameter.</summary>
    ApiKey,
    /// <summary>Bearer token in the Authorization header.</summary>
    Bearer,
    /// <summary>HTTP Basic authentication (username + password Base64-encoded).</summary>
    Basic,
    /// <summary>OAuth 2.0 Client Credentials flow with token caching and automatic refresh.</summary>
    OAuth2ClientCredentials,
    /// <summary>HMAC-signed requests (SHA-256 or SHA-512).</summary>
    Hmac,
    /// <summary>Custom auth implemented by the provider via <see cref="IAuthenticationProvider"/>.</summary>
    Custom
}
