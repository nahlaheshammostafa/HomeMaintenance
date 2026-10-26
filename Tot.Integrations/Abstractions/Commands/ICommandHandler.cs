using System.Text.Json;

namespace Tot.Integrations;

/// <summary>
/// Executes a single integration command operation.
/// Register via <c>IIntegrationsBuilder.AddCommandHandler&lt;T&gt;()</c>.
/// The operation name used for routing is determined by <see cref="Operation"/>.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// The dot-separated operation name this handler processes (e.g. <c>"access.open"</c>).
    /// Must be unique within a provider.
    /// </summary>
    string Operation { get; }

    /// <summary>
    /// Executes the command. Must be idempotent; the dispatcher may call it more than once.
    /// Return a failed <see cref="IntegrationResult{T}"/> with <c>Retryable = true</c> to schedule a retry;
    /// <c>Retryable = false</c> dead-letters the command immediately.
    /// </summary>
    /// <param name="payload">The command payload from <see cref="IntegrationCommand.Payload"/>.</param>
    /// <param name="ctx">Connection context for this command's connection.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IntegrationResult<JsonElement>> ExecuteAsync(JsonElement payload, IConnectionContext ctx, CancellationToken ct);
}
