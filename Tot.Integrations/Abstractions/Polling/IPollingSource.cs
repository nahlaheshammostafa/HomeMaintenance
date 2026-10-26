namespace Tot.Integrations;

/// <summary>
/// Implemented by a provider to support schedule-based polling for providers without webhooks.
/// The <c>PollingScheduler</c> background service calls <see cref="PollAsync"/> on each interval,
/// passing the last saved cursor. Register via <c>IIntegrationsBuilder.AddPollingSource&lt;T&gt;()</c>.
/// </summary>
public interface IPollingSource
{
    /// <summary>How often the scheduler should invoke <see cref="PollAsync"/> for each connection.</summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Polls the provider for new data since <paramref name="cursor"/>.
    /// Must be safe to call concurrently across multiple host instances;
    /// the scheduler ensures only one instance polls per (connection, source) pair via an advisory lock.
    /// </summary>
    /// <param name="ctx">Context for this connection.</param>
    /// <param name="cursor">
    /// The cursor from the last successful poll, or <see langword="null"/> on the first call.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<PollResult> PollAsync(IConnectionContext ctx, string? cursor, CancellationToken ct);
}
