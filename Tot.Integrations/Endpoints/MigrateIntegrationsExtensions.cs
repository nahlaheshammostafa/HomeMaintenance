using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tot.Integrations.Persistence;

namespace Tot.Integrations.Endpoints;

/// <summary>
/// Extension methods for applying the <c>integrations</c> schema migrations at startup.
/// </summary>
public static class MigrateIntegrationsExtensions
{
    /// <summary>
    /// Applies any pending <c>Tot.Integrations</c> database migrations and creates the
    /// <c>integrations</c> schema if it does not already exist.
    /// Call from the host's startup (after <c>Build()</c>, before <c>Run()</c>):
    /// <code>await app.Services.MigrateIntegrationsAsync();</code>
    /// Or from a dedicated migrator project — resolve <see cref="IntegrationsDbContext"/>
    /// from the host's service provider and call <c>Database.MigrateAsync()</c> directly.
    /// </summary>
    public static async Task MigrateIntegrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
