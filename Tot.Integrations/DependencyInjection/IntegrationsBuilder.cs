using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tot.Integrations.Persistence;
using Tot.Integrations.Runtime.Admin;
using Tot.Integrations.Runtime.Dispatching;
using Tot.Integrations.Runtime.Journal;
using Tot.Integrations.Runtime.Polling;
using Tot.Integrations.Runtime.Registry;
using Tot.Integrations.Runtime.Webhooks;

namespace Tot.Integrations.DependencyInjection;

internal sealed class IntegrationsBuilder : IIntegrationsBuilder
{
    private readonly ProviderRegistry _registry;
    private readonly IntegrationsOptions _options;

    public IServiceCollection Services { get; }

    internal IntegrationsBuilder(IServiceCollection services, ProviderRegistry registry, IntegrationsOptions options)
    {
        Services = services;
        _registry = registry;
        _options = options;
    }

    public IIntegrationsBuilder AddProvider<TProvider>() where TProvider : class, IIntegrationProvider
    {
        Services.TryAddTransient<TProvider>();
        _registry.RegisterProvider(typeof(TProvider));
        return this;
    }

    public IIntegrationsBuilder AddCapability<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface
    {
        Services.TryAddTransient<TInterface, TImpl>();
        Services.TryAddTransient<TImpl>();
        _registry.RegisterCapability(typeof(TInterface), typeof(TImpl));
        return this;
    }

    public IIntegrationsBuilder AddTypedClient<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface
    {
        Services.TryAddTransient<TInterface, TImpl>();
        Services.TryAddTransient<TImpl>();
        return this;
    }

    public IIntegrationsBuilder AddCommandHandler<THandler>() where THandler : class, ICommandHandler
    {
        Services.TryAddTransient<THandler>();
        _registry.RegisterCommandHandler(typeof(THandler));
        return this;
    }

    public IIntegrationsBuilder AddWebhookHandler<THandler>() where THandler : class, IWebhookHandler
    {
        Services.TryAddTransient<THandler>();
        _registry.RegisterWebhookHandler(typeof(THandler));
        return this;
    }

    public IIntegrationsBuilder AddPollingSource<TSource>() where TSource : class, IPollingSource
    {
        Services.TryAddTransient<TSource>();
        _registry.RegisterPollingSource(typeof(TSource));
        return this;
    }

    public IIntegrationsBuilder AddRedaction(params string[] patterns)
    {
        _registry.AddRedactionPatterns(patterns);
        return this;
    }

    public IIntegrationsBuilder UseSecretStore<TStore>() where TStore : class, ISecretStore
    {
        Services.AddSingleton<ISecretStore, TStore>();
        return this;
    }

    public IIntegrationsBuilder UseDispatcher<TDispatcher>() where TDispatcher : class, IIntegrationDispatcher
    {
        Services.AddSingleton<IIntegrationDispatcher, TDispatcher>();
        return this;
    }

    public IIntegrationsBuilder UsePostgres(string connectionString, string schema = "integrations")
    {
        // R-13: reject schema names that are not valid PostgreSQL identifiers
        if (!Regex.IsMatch(schema, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
            throw new ArgumentException(
                $"'{schema}' is not a valid PostgreSQL identifier. " +
                "Schema must start with a letter or underscore and contain only letters, digits, or underscores.",
                nameof(schema));

        _registry.SetConnectionString(connectionString, schema);
        Services.AddDbContext<IntegrationsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", schema)));
        Services.TryAddScoped<IConnectionStore, EfConnectionStore>();
        Services.TryAddScoped<IIntegrationCommandQueue, EfCommandQueue>();
        Services.TryAddScoped<WebhookIngestor>();
        Services.TryAddScoped<AdminApiHandler>();
        Services.TryAddSingleton<CommandDispatcher>();
        Services.TryAddSingleton<IIntegrationDispatcher>(sp => sp.GetRequiredService<CommandDispatcher>());
        Services.TryAddSingleton<WebhookProcessor>();
        Services.TryAddSingleton<PollingScheduler>();

        // R-11: only register background workers when RunBackgroundWorkers is true
        if (_options.RunBackgroundWorkers)
        {
            Services.AddHostedService<JournalWriter>();
            Services.AddHostedService(sp => sp.GetRequiredService<CommandDispatcher>());
            Services.AddHostedService(sp => sp.GetRequiredService<WebhookProcessor>());
            Services.AddHostedService(sp => sp.GetRequiredService<PollingScheduler>());
        }

        return this;
    }

    public IIntegrationsBuilder UseDataProtectionSecretStore()
    {
        Services.AddDataProtection();
        Services.TryAddScoped<ISecretStore, Tot.Integrations.Persistence.DataProtectionSecretStore>();
        return this;
    }
}
