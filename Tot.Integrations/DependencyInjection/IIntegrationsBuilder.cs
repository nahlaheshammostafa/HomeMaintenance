using Microsoft.Extensions.DependencyInjection;

namespace Tot.Integrations.DependencyInjection;

/// <summary>
/// Fluent builder returned by <c>services.AddIntegrations()</c>.
/// Use this to register providers, capabilities, command handlers, webhook handlers,
/// polling sources, custom secret stores, and custom dispatchers.
/// </summary>
public interface IIntegrationsBuilder
{
    /// <summary>The underlying service collection.</summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Registers a provider. The provider's <see cref="ProviderDescriptor"/> is read from
    /// <typeparamref name="TProvider"/> on first use.
    /// </summary>
    IIntegrationsBuilder AddProvider<TProvider>() where TProvider : class, IIntegrationProvider;

    /// <summary>
    /// Registers a capability implementation for a provider.
    /// <typeparamref name="TInterface"/> is the consuming-system's capability port;
    /// <typeparamref name="TImpl"/> is the provider's implementation.
    /// </summary>
    IIntegrationsBuilder AddCapability<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface;

    /// <summary>Registers a typed HTTP client for a provider.</summary>
    IIntegrationsBuilder AddTypedClient<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface;

    /// <summary>Registers a command handler for the given operation.</summary>
    IIntegrationsBuilder AddCommandHandler<THandler>() where THandler : class, ICommandHandler;

    /// <summary>Registers a webhook handler for a provider.</summary>
    IIntegrationsBuilder AddWebhookHandler<THandler>() where THandler : class, IWebhookHandler;

    /// <summary>Registers a polling source for a provider.</summary>
    IIntegrationsBuilder AddPollingSource<TSource>() where TSource : class, IPollingSource;

    /// <summary>
    /// Adds JSON path and header name patterns to the redaction list for the current provider.
    /// Global rules are always applied in addition to these.
    /// </summary>
    IIntegrationsBuilder AddRedaction(params string[] patterns);

    /// <summary>
    /// Replaces the default <see cref="ISecretStore"/> (Data Protection + DB)
    /// with a custom implementation.
    /// </summary>
    IIntegrationsBuilder UseSecretStore<TStore>() where TStore : class, ISecretStore;

    /// <summary>
    /// Replaces the default background-service dispatcher with a custom
    /// <see cref="IIntegrationDispatcher"/> implementation (e.g. MassTransit).
    /// </summary>
    IIntegrationsBuilder UseDispatcher<TDispatcher>() where TDispatcher : class, IIntegrationDispatcher;

    /// <summary>
    /// Configures the Postgres connection string and schema for the integrations DbContext.
    /// Call this inside the <c>configure</c> delegate passed to <c>AddIntegrations</c>.
    /// </summary>
    IIntegrationsBuilder UsePostgres(string connectionString, string schema = "integrations");

    /// <summary>
    /// Registers the default <see cref="ISecretStore"/> backed by ASP.NET Core Data Protection
    /// and the <c>integrations.secrets</c> table.
    /// </summary>
    IIntegrationsBuilder UseDataProtectionSecretStore();
}
