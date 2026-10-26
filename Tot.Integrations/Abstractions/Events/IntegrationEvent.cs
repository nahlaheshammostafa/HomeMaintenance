using System.Text.Json;

namespace Tot.Integrations;

/// <summary>
/// A domain event emitted by the integration engine after processing a webhook delivery or a poll result.
/// Business handlers receive these through <see cref="IIntegrationEventHandler"/>.
/// </summary>
/// <param name="Type">
/// Stable dot-separated event type string (e.g. <c>"access.device.state_changed"</c>).
/// </param>
/// <param name="ProviderCode">The provider that produced the event.</param>
/// <param name="ConnectionId">The connection the event arrived on.</param>
/// <param name="ProviderEventId">
/// Provider-assigned deduplication id, or <see langword="null"/> if the provider doesn't supply one.
/// </param>
/// <param name="OccurredAt">UTC timestamp when the event occurred at the provider (not when received).</param>
/// <param name="Data">Parsed event payload. Schema is defined by the provider for each <paramref name="Type"/>.</param>
public sealed record IntegrationEvent(
    string Type,
    string ProviderCode,
    Guid ConnectionId,
    string? ProviderEventId,
    DateTimeOffset OccurredAt,
    JsonElement Data);
