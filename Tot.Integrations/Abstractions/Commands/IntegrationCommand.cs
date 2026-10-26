using System.Text.Json;

namespace Tot.Integrations;

/// <summary>
/// A command with a real-world side effect that must be executed reliably (queued, retried, idempotent).
/// Enqueue via <see cref="IIntegrationCommandQueue"/>.
/// </summary>
/// <param name="ConnectionId">The connection to execute the command against.</param>
/// <param name="Operation">
/// Stable dot-separated operation name resolved to a <see cref="ICommandHandler"/> (e.g. <c>"access.open"</c>).
/// </param>
/// <param name="Payload">Serialized command payload passed to the handler.</param>
/// <param name="IdempotencyKey">
/// Caller-assigned key ensuring the command executes at most once.
/// Duplicate keys return the existing command id without re-enqueuing.
/// </param>
/// <param name="NotBefore">
/// Earliest time the dispatcher should attempt execution.
/// <see langword="null"/> means execute as soon as possible.
/// </param>
public sealed record IntegrationCommand(
    Guid ConnectionId,
    string Operation,
    JsonElement Payload,
    string IdempotencyKey,
    DateTimeOffset? NotBefore = null);
