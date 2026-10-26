using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tot.Integrations.Runtime.Auth;
using Tot.Integrations.Runtime.Handlers;
using Tot.Integrations.Runtime.Observability;
using Tot.Integrations.Runtime.Redaction;
using Tot.Integrations.Runtime.Registry;
using Tot.Integrations.Runtime.Resilience;
using Tot.Integrations.Runtime.Transport;

namespace Tot.Integrations.DependencyInjection;

/// <summary>Extension methods to register the integrations engine in a host's DI container.</summary>
public static class AddIntegrationsExtensions
{
    /// <summary>
    /// Registers the <c>Tot.Integrations</c> engine and returns a builder for adding providers,
    /// capabilities, and override registrations.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">
    /// The host's configuration root. Provider defaults are read from <c>Integrations:Providers:{code}</c>.
    /// </param>
    /// <param name="configure">Delegate to configure <see cref="IntegrationsOptions"/> and add providers.</param>
    public static IIntegrationsBuilder AddIntegrations(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IntegrationsOptions>? configure = null)
    {
        var options = new IntegrationsOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Core infrastructure (all singletons)
        var registry = new ProviderRegistry();
        services.AddSingleton(registry);
        services.AddMemoryCache();

        // Auth
        services.AddSingleton<OAuth2TokenCache>();
        services.AddSingleton<AuthProviderFactory>();

        // Resilience
        services.AddSingleton<ConnectionResilienceProvider>();
        services.AddSingleton<ConnectionRateLimiterRegistry>();

        // Journal + metrics/tracing
        services.AddSingleton<JournalChannel>();
        services.AddSingleton<Redactor>();
        services.AddSingleton<IntegrationMetrics>();

        // HTTP client for OAuth2 token fetches (separate from integration clients)
        services.AddHttpClient("integrations:oauth2");
        // Primary handler used by IntegrationHttpClientFactory
        services.AddHttpClient("integrations");

        // Per-connection client factory
        services.AddSingleton<IntegrationHttpClientFactory>();

        // Capability resolver (scoped). Registered as both concrete and interface so
        // internal services (e.g. CommandDispatcher) can resolve the concrete type directly.
        services.TryAddScoped<CapabilityResolver>();
        services.TryAddScoped<ICapabilityResolver>(sp => sp.GetRequiredService<CapabilityResolver>());

        return new IntegrationsBuilder(services, registry, options);
    }
}
