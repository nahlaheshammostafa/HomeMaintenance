using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
using Tot.Integrations.DependencyInjection;
using Tot.Integrations.Runtime.Observability;
using Tot.Integrations.Runtime.Registry;

namespace Tot.Integrations.Runtime.Webhooks;

internal sealed class WebhookProcessor : BackgroundService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan StuckLeaseTimeout = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProviderRegistry _registry;
    private readonly IntegrationsOptions _options;
    private readonly IntegrationMetrics _metrics;
    private readonly ILogger<WebhookProcessor> _logger;

    public WebhookProcessor(
        IServiceScopeFactory scopeFactory,
        ProviderRegistry registry,
        IntegrationsOptions options,
        IntegrationMetrics metrics,
        ILogger<WebhookProcessor> logger)
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
            try { await Task.Delay(_options.WebhookProcessorInterval, ct); }
            catch (OperationCanceledException) { break; }

            try { await ProcessOnceAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "WebhookProcessor: cycle threw unexpectedly.");
            }
        }
    }

    internal async Task ProcessOnceAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var connString = _registry.ConnectionString!;
        var schema = _registry.Schema;

        await RecoverStuckAsync(connString, schema, ct);
        var claimed = await ClaimAsync(connString, schema, ct);

        if (claimed.Count == 0) return;

        var connectionStore = services.GetRequiredService<IConnectionStore>();
        var capabilityResolver = services.GetRequiredService<CapabilityResolver>();
        var eventHandlers = services.GetServices<IIntegrationEventHandler>().ToList();

        foreach (var row in claimed)
            await ProcessOneAsync(row, connString, schema, connectionStore, capabilityResolver, eventHandlers, services, ct);
    }

    private async Task<List<WebhookRow>> ClaimAsync(string connString, string schema, CancellationToken ct)
    {
        var batch = _options.WebhookProcessorBatchSize;
        var rows = new List<WebhookRow>();

        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            UPDATE {schema}.inbound_webhooks
            SET status = 'Processing', attempts = attempts + 1
            WHERE id IN (
                SELECT id FROM {schema}.inbound_webhooks
                WHERE status = 'Received' AND signature_valid = true
                ORDER BY id ASC
                LIMIT {batch}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, connection_id, provider_event_id, received_at, headers, body, attempts
            """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new WebhookRow(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.GetString(4),
                (byte[])reader.GetValue(5),
                reader.GetInt32(6)));
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
            UPDATE {schema}.inbound_webhooks
            SET status = 'Received'
            WHERE status = 'Processing' AND received_at < @cutoff
            """;
        cmd.Parameters.AddWithValue("cutoff", NpgsqlDbType.TimestampTz, cutoff);
        var recovered = await cmd.ExecuteNonQueryAsync(ct);
        if (recovered > 0)
            _logger.LogWarning("WebhookProcessor: recovered {Count} stuck rows.", recovered);
    }

    private async Task ProcessOneAsync(
        WebhookRow row,
        string connString,
        string schema,
        IConnectionStore connectionStore,
        CapabilityResolver capabilityResolver,
        IReadOnlyList<IIntegrationEventHandler> eventHandlers,
        IServiceProvider services,
        CancellationToken ct)
    {
        _registry.EnsureIndexed(services);

        var connection = await connectionStore.GetByIdAsync(row.ConnectionId, ct);
        if (connection is null)
        {
            await MarkFailedAsync(connString, schema, row.Id, "Connection not found.", ct);
            _metrics.WebhooksTotal.Add(1,
                new KeyValuePair<string, object?>("provider", "unknown"),
                new KeyValuePair<string, object?>("outcome", "failed"));
            return;
        }

        var handlerType = _registry.GetWebhookHandlerTypeForProvider(connection.ProviderCode);
        if (handlerType is null)
        {
            await MarkIgnoredAsync(connString, schema, row.Id, ct);
            _metrics.WebhooksTotal.Add(1,
                new KeyValuePair<string, object?>("provider", connection.ProviderCode),
                new KeyValuePair<string, object?>("outcome", "ignored"));
            return;
        }

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(row.Headers)
                          ?? new Dictionary<string, string>();

            var hook = new InboundWebhook(
                ConnectionId: connection.Id,
                ProviderCode: connection.ProviderCode,
                Headers: headers,
                Body: row.Body,
                ReceivedAt: row.ReceivedAt);

            var ctx = await capabilityResolver.BuildConnectionContextAsync(connection, ct);
            var handler = (IWebhookHandler)Microsoft.Extensions.DependencyInjection
                .ActivatorUtilities.CreateInstance(services, handlerType);

            var events = await handler.ParseAsync(hook, ctx, ct);

            if (events.Count == 0)
            {
                await MarkIgnoredAsync(connString, schema, row.Id, ct);
                _metrics.WebhooksTotal.Add(1,
                    new KeyValuePair<string, object?>("provider", connection.ProviderCode),
                    new KeyValuePair<string, object?>("outcome", "ignored"));
                return;
            }

            foreach (var ev in events)
            {
                foreach (var h in eventHandlers.Where(x => x.CanHandle(ev)))
                {
                    try { await h.HandleAsync(ev, ct); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "WebhookProcessor: event handler {Handler} threw for webhook {Id}.",
                            h.GetType().Name, row.Id);
                    }
                }
            }

            await MarkProcessedAsync(connString, schema, row.Id, ct);
            _metrics.WebhooksTotal.Add(1,
                new KeyValuePair<string, object?>("provider", connection.ProviderCode),
                new KeyValuePair<string, object?>("outcome", "processed"));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebhookProcessor: failed to process webhook row {Id}.", row.Id);

            if (row.Attempts >= MaxAttempts)
            {
                await MarkFailedAsync(connString, schema, row.Id, ex.Message, ct);
                _metrics.WebhooksTotal.Add(1,
                    new KeyValuePair<string, object?>("provider", connection.ProviderCode),
                    new KeyValuePair<string, object?>("outcome", "failed"));
            }
            else
            {
                await ResetToReceivedAsync(connString, schema, row.Id, ex.Message, ct);
            }
        }
    }

    private static async Task MarkProcessedAsync(string cs, string schema, long id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {schema}.inbound_webhooks SET status = 'Processed' WHERE id = @id";
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task MarkIgnoredAsync(string cs, string schema, long id, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {schema}.inbound_webhooks SET status = 'Ignored' WHERE id = @id";
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task MarkFailedAsync(string cs, string schema, long id, string error, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {schema}.inbound_webhooks SET status = 'Failed', error = @err WHERE id = @id";
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);
        cmd.Parameters.AddWithValue("err", NpgsqlDbType.Text, error);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task ResetToReceivedAsync(string cs, string schema, long id, string error, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE {schema}.inbound_webhooks SET status = 'Received', error = @err WHERE id = @id";
        cmd.Parameters.AddWithValue("id", NpgsqlDbType.Bigint, id);
        cmd.Parameters.AddWithValue("err", NpgsqlDbType.Text, error);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private sealed record WebhookRow(
        long Id,
        Guid ConnectionId,
        string? ProviderEventId,
        DateTimeOffset ReceivedAt,
        string Headers,
        byte[] Body,
        int Attempts);
}
