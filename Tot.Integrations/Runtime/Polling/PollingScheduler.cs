using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Security.Cryptography;
using System.Text;
using Tot.Integrations.DependencyInjection;
using Tot.Integrations.Persistence;
using Tot.Integrations.Persistence.Entities;
using Tot.Integrations.Runtime.Observability;
using Tot.Integrations.Runtime.Registry;

namespace Tot.Integrations.Runtime.Polling;

/// <summary>
/// Background service that drives schedule-based polling for all registered
/// <see cref="IPollingSource"/> implementations across every enabled connection.
/// Uses a PostgreSQL session-level advisory lock per (connection, source) pair to
/// guarantee that only one host instance polls at a time.
/// </summary>
internal sealed class PollingScheduler : BackgroundService
{
    // How often the scheduler wakes up to see which (connection, source) pairs are due.
    internal static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProviderRegistry _registry;
    private readonly IntegrationsOptions _options;
    private readonly IntegrationMetrics _metrics;
    private readonly ILogger<PollingScheduler> _logger;

    /// <summary>Initializes a new instance of <see cref="PollingScheduler"/>.</summary>
    public PollingScheduler(
        IServiceScopeFactory scopeFactory,
        ProviderRegistry registry,
        IntegrationsOptions options,
        IntegrationMetrics metrics,
        ILogger<PollingScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_options.RunBackgroundWorkers || string.IsNullOrEmpty(_registry.ConnectionString))
            return;

        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(CheckInterval, ct); }
            catch (OperationCanceledException) { break; }

            try { await PollOnceAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "PollingScheduler: cycle threw unexpectedly.");
            }
        }
    }

    /// <summary>
    /// Runs one full check cycle: iterates every enabled connection × polling source and
    /// triggers a poll for any pair whose interval has elapsed.
    /// Exposed <c>internal</c> so integration tests can drive a single cycle.
    /// </summary>
    internal async Task PollOnceAsync(CancellationToken ct)
    {
        var sourceTypes = _registry.PollingSourceTypes;
        if (sourceTypes.Count == 0) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        _registry.EnsureIndexed(services);

        var connectionStore = services.GetRequiredService<IConnectionStore>();
        var connections = await connectionStore.ListAsync(ct: ct);
        var enabled = connections.Where(c => c.Enabled).ToList();
        if (enabled.Count == 0) return;

        var capabilityResolver = services.GetRequiredService<CapabilityResolver>();
        var eventHandlers = services.GetServices<IIntegrationEventHandler>().ToList();
        var db = services.GetRequiredService<IntegrationsDbContext>();

        foreach (var connection in enabled)
        {
            foreach (var sourceType in sourceTypes)
            {
                await TryPollAsync(connection, sourceType, capabilityResolver, eventHandlers, db, services, ct);
            }
        }
    }

    private async Task TryPollAsync(
        IntegrationConnection connection,
        Type sourceType,
        CapabilityResolver capabilityResolver,
        IReadOnlyList<IIntegrationEventHandler> eventHandlers,
        IntegrationsDbContext db,
        IServiceProvider services,
        CancellationToken ct)
    {
        var source = (IPollingSource)ActivatorUtilities.CreateInstance(services, sourceType);
        var sourceName = sourceType.FullName!;

        // Load cursor to determine if poll is due.
        var cursorEntity = await db.PollCursors
            .FirstOrDefaultAsync(p => p.ConnectionId == connection.Id && p.Source == sourceName, ct);

        if (cursorEntity?.PolledAt is not null &&
            DateTimeOffset.UtcNow - cursorEntity.PolledAt.Value < source.Interval)
            return;

        // Attempt to acquire the session-level advisory lock.
        var lockId = ComputeLockId(connection.Id, sourceName);
        var connString = _registry.ConnectionString!;

        await using var pgConn = new NpgsqlConnection(connString);
        await pgConn.OpenAsync(ct);

        if (!await TryAcquireAdvisoryLockAsync(pgConn, lockId, ct))
        {
            _logger.LogDebug(
                "PollingScheduler: advisory lock held for {Source} on connection {Id} — skipping.",
                sourceType.Name, connection.Id);
            return;
        }

        try
        {
            await ExecutePollAsync(
                connection, source, sourceName, cursorEntity, db,
                capabilityResolver, eventHandlers, services, ct);
        }
        finally
        {
            await ReleaseAdvisoryLockAsync(pgConn, lockId, ct);
        }
    }

    private async Task ExecutePollAsync(
        IntegrationConnection connection,
        IPollingSource source,
        string sourceName,
        PollCursorEntity? cursorEntity,
        IntegrationsDbContext db,
        CapabilityResolver capabilityResolver,
        IReadOnlyList<IIntegrationEventHandler> eventHandlers,
        IServiceProvider services,
        CancellationToken ct)
    {
        IConnectionContext ctx;
        try
        {
            ctx = await capabilityResolver.BuildConnectionContextAsync(connection, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "PollingScheduler: failed to build context for connection {Id}.", connection.Id);
            return;
        }

        PollResult result;
        try
        {
            result = await source.PollAsync(ctx, cursorEntity?.Cursor, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Cursor does NOT advance on failure.
            _logger.LogError(ex,
                "PollingScheduler: PollAsync threw for {Source} on connection {Id}.",
                sourceName, connection.Id);
            return;
        }

        // Persist cursor and timestamp.
        var now = DateTimeOffset.UtcNow;
        if (cursorEntity is null)
        {
            db.PollCursors.Add(new PollCursorEntity
            {
                ConnectionId = connection.Id,
                Source = sourceName,
                Cursor = result.NextCursor,
                PolledAt = now
            });
        }
        else
        {
            if (result.NextCursor is not null)
                cursorEntity.Cursor = result.NextCursor;
            cursorEntity.PolledAt = now;
        }

        await db.SaveChangesAsync(ct);
        _metrics.PollCyclesTotal.Add(1,
            new("connection_id", connection.Id),
            new("provider", connection.ProviderCode));

        // Dispatch events.
        foreach (var ev in result.Events)
            await DispatchEventAsync(eventHandlers, ev, ct);
    }

    private static async Task<bool> TryAcquireAdvisoryLockAsync(
        NpgsqlConnection conn, long lockId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT pg_try_advisory_lock(@key)";
        cmd.Parameters.AddWithValue("key", NpgsqlDbType.Bigint, lockId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static async Task ReleaseAdvisoryLockAsync(
        NpgsqlConnection conn, long lockId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_unlock(@key)";
        cmd.Parameters.AddWithValue("key", NpgsqlDbType.Bigint, lockId);
        await cmd.ExecuteScalarAsync(ct);
    }

    private static async Task DispatchEventAsync(
        IReadOnlyList<IIntegrationEventHandler> handlers,
        IntegrationEvent ev,
        CancellationToken ct)
    {
        foreach (var h in handlers.Where(x => x.CanHandle(ev)))
        {
            try { await h.HandleAsync(ev, ct); }
            catch (OperationCanceledException) { throw; }
            catch { /* event handler errors must not abort the poll */ }
        }
    }

    /// <summary>
    /// Returns a stable 64-bit advisory lock key for a (connectionId, sourceName) pair.
    /// Uses SHA-256 truncated to 8 bytes for distribution, deterministic across hosts.
    /// </summary>
    internal static long ComputeLockId(Guid connectionId, string sourceName)
    {
        var input = $"{connectionId:N}:{sourceName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToInt64(hash, 0);
    }
}
