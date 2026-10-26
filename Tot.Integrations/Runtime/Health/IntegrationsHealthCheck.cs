using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Tot.Integrations.Persistence;
using Tot.Integrations.Runtime.Registry;

namespace Tot.Integrations.Runtime.Health;

/// <summary>
/// ASP.NET Core <see cref="IHealthCheck"/> for the integrations engine.
/// Reports <see cref="HealthStatus.Unhealthy"/> when the database is unreachable, and
/// <see cref="HealthStatus.Degraded"/> when the DB is up but one or more enabled
/// connections fail their <c>PingAsync</c> check.
/// </summary>
internal sealed class IntegrationsHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProviderRegistry _registry;
    private readonly ILogger<IntegrationsHealthCheck> _logger;

    public IntegrationsHealthCheck(
        IServiceScopeFactory scopeFactory,
        ProviderRegistry registry,
        ILogger<IntegrationsHealthCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        // 1. Verify DB connectivity.
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "IntegrationsHealthCheck: database unreachable.");
            return HealthCheckResult.Unhealthy("Integrations database is unreachable.", ex);
        }

        // 2. Ping every enabled connection and collect failures.
        var failures = new List<string>();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;

            _registry.EnsureIndexed(services);

            var connectionStore = services.GetRequiredService<IConnectionStore>();
            var capabilityResolver = services.GetRequiredService<CapabilityResolver>();

            var connections = await connectionStore.ListAsync(ct: ct);

            foreach (var conn in connections.Where(c => c.Enabled))
            {
                if (!_registry.TryGetProvider(conn.ProviderCode, out var reg))
                    continue;

                try
                {
                    var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    pingCts.CancelAfter(TimeSpan.FromSeconds(5));

                    var ctx = await capabilityResolver.BuildConnectionContextAsync(conn, pingCts.Token);
                    var provider = (IIntegrationProvider)Microsoft.Extensions.DependencyInjection
                        .ActivatorUtilities.CreateInstance(services, reg.ProviderType);

                    var result = await provider.PingAsync(ctx, pingCts.Token);
                    if (!result.IsSuccess)
                    {
                        failures.Add($"{conn.Name} ({conn.ProviderCode}): {result.Error?.Message}");
                        _logger.LogWarning(
                            "IntegrationsHealthCheck: ping failed for connection {Name}: {Error}",
                            conn.Name, result.Error?.Message);
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    failures.Add($"{conn.Name} ({conn.ProviderCode}): ping timed out");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures.Add($"{conn.Name} ({conn.ProviderCode}): {ex.Message}");
                    _logger.LogWarning(ex,
                        "IntegrationsHealthCheck: ping threw for connection {Name}.", conn.Name);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "IntegrationsHealthCheck: error during connection ping sweep.");
            return HealthCheckResult.Degraded("Failed to sweep connections for health.", ex);
        }

        if (failures.Count > 0)
        {
            var data = new Dictionary<string, object>
                { ["failures"] = failures };
            return HealthCheckResult.Degraded(
                $"{failures.Count} connection(s) failed ping check.",
                data: data);
        }

        return HealthCheckResult.Healthy("All integrations connections are reachable.");
    }
}
