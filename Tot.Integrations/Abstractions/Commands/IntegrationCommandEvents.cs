using System.Text.Json;

namespace Tot.Integrations;

/// <summary>Raised by the dispatcher when a command completes successfully.</summary>
/// <param name="CommandId">The id of the completed command.</param>
/// <param name="ConnectionId">The connection the command ran against.</param>
/// <param name="Operation">The operation name that was executed.</param>
/// <param name="IdempotencyKey">The idempotency key of the command.</param>
/// <param name="Result">The result payload returned by the handler.</param>
/// <param name="CompletedAt">UTC timestamp when the command completed.</param>
public sealed record IntegrationCommandCompleted(
    Guid CommandId,
    Guid ConnectionId,
    string Operation,
    string IdempotencyKey,
    JsonElement Result,
    DateTimeOffset CompletedAt)
{
    /// <summary>Returns this event as a generic <see cref="IntegrationEvent"/> for event handlers.</summary>
    public IntegrationEvent ToEvent() => new(
        Type: "integration.command.completed",
        ProviderCode: string.Empty,
        ConnectionId: ConnectionId,
        ProviderEventId: CommandId.ToString(),
        OccurredAt: CompletedAt,
        Data: Result);
}

/// <summary>Raised by the dispatcher when a command is dead-lettered after all retries are exhausted.</summary>
/// <param name="CommandId">The id of the failed command.</param>
/// <param name="ConnectionId">The connection the command ran against.</param>
/// <param name="Operation">The operation name that failed.</param>
/// <param name="IdempotencyKey">The idempotency key of the command.</param>
/// <param name="Error">The last error that caused dead-lettering.</param>
/// <param name="FailedAt">UTC timestamp when the command was dead-lettered.</param>
public sealed record IntegrationCommandFailed(
    Guid CommandId,
    Guid ConnectionId,
    string Operation,
    string IdempotencyKey,
    IntegrationError Error,
    DateTimeOffset FailedAt)
{
    /// <summary>Returns this event as a generic <see cref="IntegrationEvent"/> for event handlers.</summary>
    public IntegrationEvent ToEvent() => new(
        Type: "integration.command.failed",
        ProviderCode: string.Empty,
        ConnectionId: ConnectionId,
        ProviderEventId: CommandId.ToString(),
        OccurredAt: FailedAt,
        Data: JsonSerializer.SerializeToElement(Error));
}
