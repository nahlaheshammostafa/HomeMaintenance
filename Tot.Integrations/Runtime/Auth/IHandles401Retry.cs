namespace Tot.Integrations.Runtime.Auth;

/// <summary>
/// Optional contract for auth providers that can invalidate a cached token and retry
/// after a 401 Unauthorized response from the vendor API.
/// Implemented by <see cref="OAuth2ClientCredentialsProvider"/>.
/// </summary>
internal interface IHandles401Retry
{
    void InvalidateCache();
}
