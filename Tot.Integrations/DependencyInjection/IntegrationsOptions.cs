namespace Tot.Integrations.DependencyInjection;

/// <summary>
/// Top-level configuration for the integrations engine.
/// Passed to <c>services.AddIntegrations(configuration, configure)</c>.
/// </summary>
public sealed class IntegrationsOptions
{
    /// <summary>PostgreSQL connection string for the <c>integrations</c> schema.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>PostgreSQL schema name. Defaults to <c>"integrations"</c>.</summary>
    public string Schema { get; set; } = "integrations";

    /// <summary>
    /// When <see langword="true"/> the host registers and starts the background workers:
    /// command dispatcher, webhook processor, polling scheduler and journal writer.
    /// Set to <see langword="false"/> on hosts that only enqueue commands.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool RunBackgroundWorkers { get; set; } = true;

    /// <summary>How often the command dispatcher polls for due commands. Defaults to 2 seconds.</summary>
    public TimeSpan DispatcherInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum commands to claim per dispatcher cycle. Defaults to 20.</summary>
    public int DispatcherBatchSize { get; set; } = 20;

    /// <summary>How often the webhook processor polls for unprocessed webhooks. Defaults to 2 seconds.</summary>
    public TimeSpan WebhookProcessorInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Maximum webhook rows to claim per processor cycle. Defaults to 20.</summary>
    public int WebhookProcessorBatchSize { get; set; } = 20;

    /// <summary>Number of days to retain journal (<c>integration_calls</c>) rows. Defaults to 30.</summary>
    public int JournalRetentionDays { get; set; } = 30;

    /// <summary>Default per-attempt HTTP timeout. Defaults to 10 seconds.</summary>
    public TimeSpan DefaultAttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Default total HTTP timeout including all retries. Defaults to 30 seconds.</summary>
    public TimeSpan DefaultTotalTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
