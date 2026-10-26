namespace Tot.Integrations;

/// <summary>
/// Marker interface every provider must implement.
/// A provider describes itself via <see cref="Descriptor"/> and optionally supports a connectivity check.
/// </summary>
public interface IIntegrationProvider
{
    /// <summary>Static description of this provider's capabilities and configuration contract.</summary>
    ProviderDescriptor Descriptor { get; }

    /// <summary>
    /// Performs a cheap authenticated request to verify that <paramref name="ctx"/>'s credentials
    /// are valid and the provider API is reachable.
    /// Used by "Test connection" in the admin UI and the health check.
    /// </summary>
    /// <param name="ctx">Context carrying the connection and pre-configured HTTP client.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IntegrationResult<HealthInfo>> PingAsync(IConnectionContext ctx, CancellationToken ct);
}
