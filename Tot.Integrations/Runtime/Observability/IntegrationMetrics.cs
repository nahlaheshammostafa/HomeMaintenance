using System.Diagnostics.Metrics;
using Tot.Integrations.Runtime.Handlers;

namespace Tot.Integrations.Runtime.Observability;

/// <summary>
/// Singleton that owns the <c>Tot.Integrations</c> <see cref="Meter"/> and all its instruments.
/// Register with <c>services.AddSingleton&lt;IntegrationMetrics&gt;()</c> after the meter has been
/// added to the host's metrics pipeline via <c>builder.Services.AddOpenTelemetryMetrics(…)</c>
/// or a <c>MeterListener</c> in tests.
/// </summary>
internal sealed class IntegrationMetrics : IDisposable
{
    /// <summary>The <see cref="Meter"/> name exposed to OpenTelemetry / Prometheus collectors.</summary>
    public const string MeterName = "Tot.Integrations";

    private readonly Meter _meter;

    /// <summary>Total outbound HTTP requests by provider, operation, and outcome.</summary>
    internal readonly Counter<long> RequestsTotal;

    /// <summary>Duration (ms) of outbound HTTP requests.</summary>
    internal readonly Histogram<double> RequestDurationMs;

    /// <summary>Commands successfully executed.</summary>
    internal readonly Counter<long> CommandsSucceededTotal;

    /// <summary>Commands permanently moved to dead-letter status.</summary>
    internal readonly Counter<long> CommandsDeadletteredTotal;

    /// <summary>Inbound webhooks received by provider and signature validity.</summary>
    internal readonly Counter<long> WebhooksTotal;

    /// <summary>Completed poll cycles by connection and provider.</summary>
    internal readonly Counter<long> PollCyclesTotal;

    /// <summary>
    /// Initialises all instruments and wires the observable counter that reads
    /// <see cref="JournalChannel.DroppedCount"/> without blocking the call path.
    /// </summary>
    public IntegrationMetrics(JournalChannel journalChannel)
    {
        _meter = new Meter(MeterName, "1.0.0");

        RequestsTotal = _meter.CreateCounter<long>(
            "integration_requests_total",
            unit: "{request}",
            description: "Total outbound integration HTTP requests by provider, operation, and outcome.");

        RequestDurationMs = _meter.CreateHistogram<double>(
            "integration_request_duration_ms",
            unit: "ms",
            description: "Duration of outbound integration HTTP requests in milliseconds.");

        CommandsSucceededTotal = _meter.CreateCounter<long>(
            "integration_commands_succeeded_total",
            unit: "{command}",
            description: "Total integration commands that completed successfully.");

        CommandsDeadletteredTotal = _meter.CreateCounter<long>(
            "integration_commands_deadlettered_total",
            unit: "{command}",
            description: "Total integration commands that have been dead-lettered after all retries.");

        WebhooksTotal = _meter.CreateCounter<long>(
            "integration_webhooks_total",
            unit: "{webhook}",
            description: "Total inbound integration webhooks received.");

        PollCyclesTotal = _meter.CreateCounter<long>(
            "integration_poll_cycles_total",
            unit: "{cycle}",
            description: "Total completed poll cycles by connection and provider.");

        // Observable counter — reads the channel's drop counter without any lock.
        _meter.CreateObservableCounter<long>(
            "integration_journal_dropped_total",
            () => Volatile.Read(ref journalChannel.DroppedCount),
            unit: "{record}",
            description: "Total call-journal records dropped because the bounded channel was full.");
    }

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
