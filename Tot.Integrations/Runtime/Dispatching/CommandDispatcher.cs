using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
using Tot.Integrations.DependencyInjection;
using Tot.Integrations.Persistence;
using Tot.Integrations.Runtime.Observability;
using Tot.Integrations.Runtime.Registry;

namespace Tot.Integrations.Runtime.Dispatching;

internal sealed class CommandDispatcher : BackgroundService, IIntegrationDispatcher
{
    // Exponential-ish backoff schedule: 1m, 5m, 15m, 1h, 6h (capped).
    // The ix_commands_pending index (next_attempt_at, status WHERE status IN ('Pending','Running'))
    // must cover next_attempt_at to avoid a seq-scan on every dispatch loop tick.
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6)
    ];
    private const int MaxAttempts = 5;
    private static readonly TimeSpan StuckLeaseTimeout = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProviderRegistry _registry;
    private readonly IntegrationsOptions _options;
    private readonly IntegrationMetrics _metrics;
    private readonly ILogger<CommandDispatcher> _logger;
    private readonly SemaphoreSlim _trigger = new(0, 1);

    public CommandDispatcher(
        IServiceScopeFactory scopeFactory,
        ProviderRegistry registry,
        IntegrationsOptions options,
        IntegrationMetrics metrics,
        ILogger<CommandDispatcher> logger)
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
            try
            {
                await _trigger.WaitAsync(_options.DispatcherInterval, ct);
            }
            catch (OperationCanceledException) { break; }

            try { await DispatchNowAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "CommandDispatcher: dispatch cycle threw unexpectedly.");
            }
        }
    }

    /// <inheritdoc/>
    public async Task DispatchNowAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // No-op when called without a database (e.g. in unit tests without UsePostgres).
        if (string.IsNullOrEmpty(_registry.ConnectionString))
            return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var connString = _registry.ConnectionString;
        var schema = _registry.Schema;

        await RecoverStuckAsync(connString, schema, ct);
        var claimed = await ClaimAsync(connString, schema, ct);

        if (claimed.Count == 0) return;

        var connectionStore = services.GetRequiredService<IConnectionStore>();
        var capabilityResolver = services.GetRequiredService<CapabilityResolver>();
        var eventHandlers = services.GetServices<IIntegrationEventHandler>().ToList();

        foreach (var row in claimed)
            await ExecuteOneAsync(row, connString, schema, connectionStore, capabilityResolver, eventHandlers, services, ct);
    }

    private async Task<List<DispatchRow>> ClaimAsync(string connString, string schema, CancellationToken ct)
    {
        var batch = _options.DispatcherBatchSize;
        var rows = new List<DispatchRow>();

        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE {schema}.commands
            SET status = 'Running', locked_at = now(), attempts = attempts + 1
            WHERE id IN (
                SELECT id FROM {schema}.commands
                WHERE status = 'Pending'
                  AND (next_attempt_at IS NULL OR next_attempt_at <= now())
                ORDER BY next_attempt_at ASC NULLS FIRST
                LIMIT {batch}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, connection_id, operation, idempotency_key, payload, attempts, correlation_id
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new DispatchRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    private async Task RecoverStuckAsync(string connString, string schema, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - StuckLeaseTimeout;
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE {schema}.commands
            SET status = 'Pending', next_attempt_at = now()
            WHERE status = 'Running' AND locked_at < @cutoff
            """;
        cmd.Parameters.AddWithValue("cutoff", NpgsqlDbType.TimestampTz, cutoff);
        var recovered = await cmd.ExecuteNonQueryAsync(ct);
        if (recovered > 0)
            _logger.LogWarning("CommandDispatcher: recovered {Count} stuck commands.", recovered);
    }

    private async Task ExecuteOneAsync(
        DispatchRow row,
        string connString,
        string schema,
        IConnectionStore connectionStore,
        CapabilityResolver capabilityResolver,
        IReadOnlyList<IIntegrationEventHandler> eventHandlers,
        IServiceProvider services,
        CancellationToken ct)
    {
        var handlerType = _registry.GetHandlerTypeForOperation(row.Operation);
        if (handlerType is null)
        {
            _logger.LogError("CommandDispatcher: no handler registered for operation '{Op}'.", row.Operation);
            var noHandlerErr = new IntegrationError(ErrorKind.Unknown, "NO_HANDLER",
                $"No handler for operation '{row.Operation}'.", false);
            await MarkDeadLetteredAsync(connString, schema, row.Id, noHandlerErr, ct);
            _metrics.CommandsDeadletteredTotal.Add(1);
            await RaiseFailedEventAsync(eventHandlers, row, noHandlerErr, ct);
            return;
        }

        IntegrationResult<JsonElement> result;
        try
        {
            var connection = await connectionStore.GetByIdAsync(row.ConnectionId, ct);
            if (connection is null)
            {
                await MarkDeadLetteredAsync(connString, schema, row.Id,
                    new IntegrationError(ErrorKind.Unknown, "CONN_NOT_FOUND",
                        $"Connection '{row.ConnectionId}' not found.", false), ct);
                _metrics.CommandsDeadletteredTotal.Add(1);
                return;
            }

            var ctx = await capabilityResolver.BuildConnectionContextAsync(connection, ct);
            var handler = (ICommandHandler)Microsoft.Extensions.DependencyInjection
                .ActivatorUtilities.CreateInstance(services, handlerType);
            var payload = JsonDocument.Parse(row.Payload).RootElement;
            result = await handler.ExecuteAsync(payload, ctx, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            result = IntegrationResult<JsonElement>.FromException(ex);
        }

        if (result.IsSuccess)
        {
            await MarkSucceededAsync(connString, schema, row.Id, result.Value, ct);
            _metrics.CommandsSucceededTotal.Add(1);
            await RaiseCompletedEventAsync(eventHandlers, row, result.Value, ct);
        }
        else if (!result.Error!.Retryable || row.Attempts >= MaxAttempts)
        {
            await MarkDeadLetteredAsync(connString, schema, row.Id, result.Error, ct);
            _metrics.CommandsDeadletteredTotal.Add(1);
            await RaiseFailedEventAsync(eventHandlers, row, result.Error, ct);
        }
        else
        {
            var delay = row.Attempts <= RetryDelays.Length
                ? RetryDelays[row.Attempts - 1]
                : RetryDelays[^1];
            await ScheduleRetryAsync(connString, schema, row.Id, result.Error, delay, ct);
        }
    }

    private static async Task MarkSucceededAsync(
        string cs, string schema, Guid id, JsonElement resultValue, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE {schema}.commands
            SET status = 'Succeeded', result = @result::jsonb, completed_at = now()
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);
        cmd.Parameters.AddWithValue("result", NpgsqlDbType.Text,
            resultValue.ValueKind == JsonValueKind.Undefined ? "{}" : resultValue.GetRawText());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task MarkDeadLetteredAsync(
        string cs, string schema, Guid id, IntegrationError error, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE {schema}.commands
            SET status = 'DeadLettered', last_error = @err::jsonb, completed_at = now()
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);
        cmd.Parameters.AddWithValue("err", NpgsqlDbType.Text, JsonSerializer.Serialize(error));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task ScheduleRetryAsync(
        string cs, string schema, Guid id, IntegrationError error, TimeSpan delay, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE {schema}.commands
            SET status = 'Pending', next_attempt_at = @next, last_error = @err::jsonb
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);
        cmd.Parameters.AddWithValue("next", NpgsqlDbType.TimestampTz, DateTimeOffset.UtcNow + delay);
        cmd.Parameters.AddWithValue("err", NpgsqlDbType.Text, JsonSerializer.Serialize(error));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task RaiseCompletedEventAsync(
        IReadOnlyList<IIntegrationEventHandler> handlers,
        DispatchRow row,
        JsonElement result,
        CancellationToken ct)
    {
        var completed = new IntegrationCommandCompleted(
            row.Id, row.ConnectionId, row.Operation, row.IdempotencyKey, result, DateTimeOffset.UtcNow);
        await DispatchEventAsync(handlers, completed.ToEvent(), ct);
    }

    private static async Task RaiseFailedEventAsync(
        IReadOnlyList<IIntegrationEventHandler> handlers,
        DispatchRow row,
        IntegrationError error,
        CancellationToken ct)
    {
        var failed = new IntegrationCommandFailed(
            row.Id, row.ConnectionId, row.Operation, row.IdempotencyKey, error, DateTimeOffset.UtcNow);
        await DispatchEventAsync(handlers, failed.ToEvent(), ct);
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
            catch { /* event handler errors must not fail the command */ }
        }
    }

    private sealed record DispatchRow(
        Guid Id,
        Guid ConnectionId,
        string Operation,
        string IdempotencyKey,
        string Payload,
        int Attempts,
        string? CorrelationId);
}
