using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tot.Integrations.Persistence;
using Tot.Integrations.Runtime.Auth;
using Tot.Integrations.Runtime.Connections;
using Tot.Integrations.Runtime.Transport;

namespace Tot.Integrations.Runtime.Registry;

internal sealed class CapabilityResolver : ICapabilityResolver
{
    private readonly IConnectionStore _connections;
    private readonly ProviderRegistry _registry;
    private readonly IServiceProvider _services;
    private readonly IMemoryCache _cache;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AuthProviderFactory _authFactory;
    private readonly IntegrationHttpClientFactory _clientFactory;
    private readonly ISecretStore _secretStore;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public CapabilityResolver(
        IConnectionStore connections,
        ProviderRegistry registry,
        IServiceProvider services,
        IMemoryCache cache,
        ILoggerFactory loggerFactory,
        AuthProviderFactory authFactory,
        IntegrationHttpClientFactory clientFactory,
        ISecretStore secretStore)
    {
        _connections = connections;
        _registry = registry;
        _services = services;
        _cache = cache;
        _loggerFactory = loggerFactory;
        _authFactory = authFactory;
        _clientFactory = clientFactory;
        _secretStore = secretStore;
    }

    public async Task<TCapability> GetAsync<TCapability>(Guid connectionId, CancellationToken ct)
        where TCapability : class
    {
        ct.ThrowIfCancellationRequested();

        var connection = await GetConnectionAsync(connectionId, ct);

        if (!connection.Enabled)
            throw new InvalidOperationException(
                $"Connection '{connectionId}' ({connection.Name}) is disabled.");

        return await ResolveAsync<TCapability>(connection, ct);
    }

    public async Task<IReadOnlyList<(IntegrationConnection Connection, TCapability Capability)>> GetAllAsync<TCapability>(
        string? ownerType,
        string? ownerId,
        CancellationToken ct)
        where TCapability : class
    {
        var all = await _connections.ListAsync(ownerType: ownerType, ownerId: ownerId, ct: ct);
        var result = new List<(IntegrationConnection, TCapability)>();

        foreach (var conn in all.Where(c => c.Enabled))
        {
            try
            {
                var cap = await ResolveAsync<TCapability>(conn, ct);
                result.Add((conn, cap));
            }
            catch (InvalidOperationException ex)
            {
                var logger = _loggerFactory.CreateLogger<CapabilityResolver>();
                logger.LogDebug(ex, "Skipping connection {ConnectionId} for capability {Capability}",
                    conn.Id, typeof(TCapability).Name);
            }
        }

        return result;
    }

    internal void InvalidateCache(Guid connectionId) =>
        _cache.Remove($"integration:connection:{connectionId}");

    /// <summary>
    /// Builds an <see cref="IConnectionContext"/> for the given connection without requiring a
    /// specific capability interface. Used by the command dispatcher (INT-21) to create
    /// the context it passes to <see cref="ICommandHandler.ExecuteAsync"/>.
    /// </summary>
    internal async Task<IConnectionContext> BuildConnectionContextAsync(
        IntegrationConnection connection, CancellationToken ct)
    {
        _registry.EnsureIndexed(_services);

        if (!_registry.TryGetProvider(connection.ProviderCode, out var reg))
            throw new InvalidOperationException($"Provider '{connection.ProviderCode}' is not registered.");

        var authProvider = await _authFactory.CreateAsync(
            connection, reg.Descriptor.Auth, _secretStore, ct);

        var baseUrl = ExtractBaseUrl(connection.Settings);
        var http = _clientFactory.CreateClient(connection, authProvider, baseUrl);

        var dbContext = _services.GetService<IntegrationsDbContext>();
        IExternalIdMap idMap = dbContext is not null
            ? new EfExternalIdMap(dbContext, connection.Id)
            : new NoOpExternalIdMap();

        var logger = _loggerFactory.CreateLogger(connection.ProviderCode);
        return new ConnectionContext(connection, http, idMap, logger);
    }

    private async Task<IntegrationConnection> GetConnectionAsync(Guid id, CancellationToken ct)
    {
        var cacheKey = $"integration:connection:{id}";

        if (_cache.TryGetValue(cacheKey, out IntegrationConnection? cached) && cached is not null)
            return cached;

        var connection = await _connections.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Integration connection '{id}' not found.");

        _cache.Set(cacheKey, connection, CacheTtl);
        return connection;
    }

    private async Task<TCapability> ResolveAsync<TCapability>(IntegrationConnection connection, CancellationToken ct)
        where TCapability : class
    {
        _registry.EnsureIndexed(_services);

        var implType = _registry.GetCapabilityImpl(typeof(TCapability))
            ?? throw new InvalidOperationException(
                $"No implementation registered for capability '{typeof(TCapability).Name}'. " +
                $"Register it with .AddCapability<{typeof(TCapability).Name}, TImpl>().");

        if (!_registry.TryGetProvider(connection.ProviderCode, out var reg))
            throw new InvalidOperationException(
                $"Provider '{connection.ProviderCode}' is not registered.");

        // Build auth provider for this connection
        var authProvider = await _authFactory.CreateAsync(
            connection, reg.Descriptor.Auth, _secretStore, ct);

        // Build base URL from connection settings or options
        var baseUrl = ExtractBaseUrl(connection.Settings);

        // Create the per-connection HttpClient with the full pipeline
        var http = _clientFactory.CreateClient(connection, authProvider, baseUrl);

        var dbContext = _services.GetService<IntegrationsDbContext>();
        IExternalIdMap idMap = dbContext is not null
            ? new EfExternalIdMap(dbContext, connection.Id)
            : new NoOpExternalIdMap();

        var logger = _loggerFactory.CreateLogger(implType);
        var ctx = new ConnectionContext(connection, http, idMap, logger);

        // Create the capability implementation, injecting IConnectionContext as an overriding arg
        var impl = Microsoft.Extensions.DependencyInjection.ActivatorUtilities
            .CreateInstance(_services, implType, ctx);

        return (TCapability)impl;
    }

    private static string? ExtractBaseUrl(System.Text.Json.JsonElement settings)
    {
        if (settings.TryGetProperty("baseUrl", out var el) ||
            settings.TryGetProperty("BaseUrl", out el) ||
            settings.TryGetProperty("base_url", out el))
        {
            if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                return el.GetString();
        }
        return null;
    }
}

/// <summary>Stub id map until INT-19 ships the EF implementation.</summary>
internal sealed class NoOpExternalIdMap : IExternalIdMap
{
    public Task<ExternalRef?> GetExternalAsync(string internalType, string internalId, string externalType, CancellationToken ct)
        => Task.FromResult<ExternalRef?>(null);
    public Task<(string InternalType, string InternalId)?> GetInternalAsync(ExternalRef externalRef, CancellationToken ct)
        => Task.FromResult<(string, string)?>(null);
    public Task UpsertAsync(string internalType, string internalId, ExternalRef externalRef,
        System.Text.Json.JsonElement? meta, CancellationToken ct)
        => Task.CompletedTask;
    public Task DeleteAsync(string internalType, string internalId, string externalType, CancellationToken ct)
        => Task.CompletedTask;
}
