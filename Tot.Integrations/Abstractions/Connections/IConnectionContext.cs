using Microsoft.Extensions.Logging;

namespace Tot.Integrations;

/// <summary>
/// Provides everything a provider implementation needs to handle one operation:
/// the resolved connection, a pre-configured HTTP client, id mapping, and logging.
/// A new instance is created per capability resolution.
/// </summary>
public interface IConnectionContext
{
    /// <summary>The connection this context is scoped to.</summary>
    IntegrationConnection Connection { get; }

    /// <summary>
    /// Deserializes the connection's non-secret settings into <typeparamref name="TSettings"/>.
    /// </summary>
    /// <typeparam name="TSettings">The provider-specific settings type declared in its <see cref="ProviderDescriptor"/>.</typeparam>
    TSettings Settings<TSettings>() where TSettings : class;

    /// <summary>
    /// An <see cref="HttpClient"/> pre-configured with auth, resilience, correlation and journal
    /// handlers for this connection. Providers should not create their own clients.
    /// </summary>
    HttpClient Http { get; }

    /// <summary>External-id mapping scoped to this connection.</summary>
    IExternalIdMap Ids { get; }

    /// <summary>A logger with <c>ProviderCode</c> and <c>ConnectionId</c> scope properties pre-attached.</summary>
    ILogger Logger { get; }
}
