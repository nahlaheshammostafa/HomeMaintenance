using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tot.Integrations.Runtime.Health;

namespace Tot.Integrations.DependencyInjection;

/// <summary>
/// Extension methods for registering the integrations health check.
/// </summary>
public static class IntegrationsHealthExtensions
{
    /// <summary>
    /// Adds the <c>Tot.Integrations</c> health check to the <see cref="IHealthChecksBuilder"/>.
    /// The check verifies DB connectivity and pings every enabled connection.
    /// Returns <see cref="HealthStatus.Degraded"/> when the database is reachable but at least
    /// one connection's <c>PingAsync</c> fails; <see cref="HealthStatus.Unhealthy"/> when the
    /// database cannot be reached.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Health check name (default: <c>"integrations"</c>).</param>
    /// <param name="failureStatus">
    /// Optional override for the reported status on failure.
    /// <see langword="null"/> uses the check's natural result.
    /// </param>
    /// <param name="tags">Optional tags for filtering.</param>
    public static IHealthChecksBuilder AddIntegrationsHealth(
        this IHealthChecksBuilder builder,
        string name = "integrations",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        builder.Services.AddSingleton<IntegrationsHealthCheck>();

        builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<IntegrationsHealthCheck>(),
            failureStatus,
            tags));

        return builder;
    }
}
