using Microsoft.Extensions.DependencyInjection;
using Tot.Integrations;
using Tot.Integrations.DependencyInjection;

namespace HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

/// <summary>
/// Registration extensions for adding HomeDispatch provider into Tot.Integrations.
/// </summary>
public static class HomeDispatchExtensions
{
    public static IIntegrationsBuilder AddHomeDispatch(this IIntegrationsBuilder builder)
    {
        builder.AddProvider<HomeDispatchProvider>();
        builder.AddCapability<IDispatchCapability, DispatchCapability>();
        builder.AddCommandHandler<DispatchTechnicianCommandHandler>();
        builder.AddWebhookHandler<HomeDispatchWebhookHandler>();

        // Register domain event listener
        builder.Services.AddScoped<IIntegrationEventHandler, HomeDispatchEventHandler>();

        return builder;
    }
}
