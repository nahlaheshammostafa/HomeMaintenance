namespace Tot.Integrations;

/// <summary>
/// Applies authentication to an outbound HTTP request for a specific connection.
/// Implement this interface for custom auth schemes; built-in schemes are handled by the runtime.
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Mutates <paramref name="request"/> to add authentication headers or query parameters.
    /// Called by the runtime's <c>AuthHandler</c> delegating handler before the request is sent.
    /// </summary>
    /// <param name="request">The outbound request to authenticate.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AuthenticateAsync(HttpRequestMessage request, CancellationToken ct);
}
