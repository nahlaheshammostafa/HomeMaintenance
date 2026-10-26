using System.Text.Json;

namespace Tot.Integrations.Endpoints;

/// <summary>Payload for creating a new integration connection. <c>SecretValue</c> is write-only.</summary>
public sealed record CreateConnectionRequest(
    string ProviderCode,
    string Name,
    string? OwnerType,
    string? OwnerId,
    string Environment,
    JsonElement Settings,
    string SecretValue);

/// <summary>Payload for updating a connection (everything except secret and provider code).</summary>
public sealed record UpdateConnectionRequest(
    string Name,
    string? OwnerType,
    string? OwnerId,
    string Environment,
    JsonElement Settings);

/// <summary>Payload for rotating the connection secret. The value is never echoed back.</summary>
public sealed record SetSecretRequest(string SecretValue);

/// <summary>Payload for upserting an external ID mapping.</summary>
public sealed record UpsertExternalIdRequest(
    string InternalType,
    string InternalId,
    string ExternalType,
    string ExternalId,
    JsonElement? Meta = null);

/// <summary>Connection data returned by admin endpoints. Secret references are never included.</summary>
public sealed record ConnectionDto(
    Guid Id,
    string ProviderCode,
    string Name,
    string? OwnerType,
    string? OwnerId,
    string Environment,
    bool Enabled,
    JsonElement Settings,
    string WebhookKey);

/// <summary>External ID mapping as returned by admin endpoints.</summary>
public sealed record ExternalIdDto(
    Guid ConnectionId,
    string InternalType,
    string InternalId,
    string ExternalType,
    string ExternalId,
    JsonElement? Meta);

/// <summary>Command row summary as returned by admin endpoints.</summary>
public sealed record CommandDto(
    Guid Id,
    Guid ConnectionId,
    string Operation,
    string IdempotencyKey,
    string Status,
    int Attempts,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Inbound webhook row summary as returned by admin endpoints.</summary>
public sealed record WebhookSummaryDto(
    long Id,
    Guid ConnectionId,
    string? ProviderEventId,
    DateTimeOffset ReceivedAt,
    bool SignatureValid,
    string Status,
    int Attempts,
    string? Error);

/// <summary>Provider descriptor summary as returned by admin endpoints.</summary>
public sealed record ProviderDto(
    string Code,
    string DisplayName,
    string Category,
    string Auth,
    bool SupportsWebhooks,
    bool SupportsPolling,
    string[] Capabilities);
