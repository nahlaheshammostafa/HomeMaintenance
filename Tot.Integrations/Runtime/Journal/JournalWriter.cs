using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Tot.Integrations.DependencyInjection;
using Tot.Integrations.Runtime.Handlers;
using Tot.Integrations.Runtime.Redaction;
using Tot.Integrations.Runtime.Registry;

namespace Tot.Integrations.Runtime.Journal;

/// <summary>
/// Background service that drains <see cref="JournalChannel"/> to the
/// <c>integrations.calls</c> partitioned table via Npgsql binary COPY.
/// Also maintains daily partitions: creates 7 days ahead, drops rows older
/// than <see cref="IntegrationsOptions.JournalRetentionDays"/>.
/// </summary>
internal sealed class JournalWriter : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromHours(1);
    private const int BatchSize = 1_000;
    private const int PartitionsDaysAhead = 7;

    private readonly JournalChannel _channel;
    private readonly string? _connectionString;
    private readonly string _schema;
    private readonly int _retentionDays;
    private readonly bool _runWorkers;
    private readonly ILogger<JournalWriter> _logger;

    public JournalWriter(
        JournalChannel channel,
        ProviderRegistry registry,
        IntegrationsOptions options,
        ILogger<JournalWriter> logger)
    {
        _channel = channel;
        _connectionString = registry.ConnectionString;
        _schema = registry.Schema;
        _retentionDays = options.JournalRetentionDays;
        _runWorkers = options.RunBackgroundWorkers;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_runWorkers || string.IsNullOrEmpty(_connectionString))
            return;

        await EnsurePartitionsAsync(ct);
        var nextMaintenance = DateTime.UtcNow + MaintenanceInterval;

        while (!ct.IsCancellationRequested)
        {
            var batch = await CollectBatchAsync(ct);

            if (batch.Count > 0)
                await WriteBatchAsync(batch, ct);

            if (DateTime.UtcNow >= nextMaintenance)
            {
                await EnsurePartitionsAsync(ct);
                nextMaintenance = DateTime.UtcNow + MaintenanceInterval;
            }
        }
    }

    private async Task<List<CallRecord>> CollectBatchAsync(CancellationToken ct)
    {
        var batch = new List<CallRecord>(BatchSize);
        using var flushCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        flushCts.CancelAfter(FlushInterval);

        try
        {
            await foreach (var record in _channel.Reader.ReadAllAsync(flushCts.Token))
            {
                batch.Add(record);
                if (batch.Count >= BatchSize) break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 1-second flush timeout — write whatever accumulated
        }

        return batch;
    }

    private async Task WriteBatchAsync(IReadOnlyList<CallRecord> batch, CancellationToken ct)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var writer = await conn.BeginBinaryImportAsync(
                $"""
                COPY {_schema}.calls (
                    ts, connection_id, provider_code, operation, method, url_path,
                    status_code, duration_ms, outcome, error_kind,
                    request_body, response_body, trace_id, command_id
                ) FROM STDIN (FORMAT BINARY)
                """, ct);

            foreach (var r in batch)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(r.Ts, NpgsqlDbType.TimestampTz, ct);
                await writer.WriteAsync(r.ConnectionId, NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(r.ProviderCode, NpgsqlDbType.Text, ct);
                await WriteNullableAsync(writer, r.Operation, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(r.Method, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(r.UrlPath, NpgsqlDbType.Text, ct);
                await WriteNullableAsync(writer, r.StatusCode, NpgsqlDbType.Integer, ct);
                await writer.WriteAsync(r.DurationMs, NpgsqlDbType.Integer, ct);
                await writer.WriteAsync(r.Outcome, NpgsqlDbType.Text, ct);
                await WriteNullableAsync(writer, r.ErrorKind, NpgsqlDbType.Text, ct);
                await WriteNullableAsync(writer, r.RequestBody, NpgsqlDbType.Jsonb, ct);
                await WriteNullableAsync(writer, r.ResponseBody, NpgsqlDbType.Jsonb, ct);
                await WriteNullableAsync(writer, r.TraceId, NpgsqlDbType.Text, ct);
                await WriteNullableAsync(writer, r.CommandId, NpgsqlDbType.Uuid, ct);
            }

            await writer.CompleteAsync(ct);
            _logger.LogDebug("Journal: wrote {Count} call records", batch.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Journal: failed to write batch of {Count} records; records discarded.", batch.Count);
        }
    }

    private async Task EnsurePartitionsAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_connectionString)) return;

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            for (int i = 0; i <= PartitionsDaysAhead; i++)
            {
                var date = today.AddDays(i);
                var partName = $"calls_{date:yyyy_MM_dd}";
                var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    CREATE TABLE IF NOT EXISTS {QuoteIdentifier(_schema)}.{QuoteIdentifier(partName)}
                    PARTITION OF {QuoteIdentifier(_schema)}.calls
                    FOR VALUES FROM ('{start.ToString("O", CultureInfo.InvariantCulture)}') TO ('{end.ToString("O", CultureInfo.InvariantCulture)}')
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await DropOldPartitionsAsync(conn, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Journal: partition maintenance failed; will retry at next interval.");
        }
    }

    private async Task DropOldPartitionsAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-_retentionDays));
        var cutoffName = $"calls_{cutoff:yyyy_MM_dd}";

        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = $"""
            SELECT c.relname
            FROM pg_inherits i
            JOIN pg_class c ON c.oid = i.inhrelid
            JOIN pg_class p ON p.oid = i.inhparent
            JOIN pg_namespace n ON n.oid = p.relnamespace
            WHERE n.nspname = @schema AND p.relname = 'calls'
              AND c.relname < @cutoff
            """;
        listCmd.Parameters.AddWithValue("schema", _schema);
        listCmd.Parameters.AddWithValue("cutoff", cutoffName);

        var toDrop = new List<string>();
        await using (var reader = await listCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                toDrop.Add(reader.GetString(0));
        }

        foreach (var partition in toDrop)
        {
            await using var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = $"DROP TABLE IF EXISTS {QuoteIdentifier(_schema)}.{QuoteIdentifier(partition)}";
            await dropCmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("Journal: dropped old partition {Partition}", partition);
        }
    }

    // internal for unit-testing without a live DB
    internal static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static async Task WriteNullableAsync<T>(
        NpgsqlBinaryImporter writer, T? value, NpgsqlDbType type, CancellationToken ct)
        where T : struct
    {
        if (value is null)
            await writer.WriteNullAsync(ct);
        else
            await writer.WriteAsync(value.Value, type, ct);
    }

    private static async Task WriteNullableAsync(
        NpgsqlBinaryImporter writer, string? value, NpgsqlDbType type, CancellationToken ct)
    {
        if (value is null)
            await writer.WriteNullAsync(ct);
        else
            await writer.WriteAsync(value, type, ct);
    }
}
