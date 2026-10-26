using System.Text.Json;

namespace Tot.Integrations;

/// <summary>
/// Represents a configured connection to an external provider.
/// One provider may have multiple connections (e.g. sandbox vs. production, or per-landlord).
/// </summary>
/// <param name="Id">Stable unique identifier for this connection.</param>
/// <param name="ProviderCode">The provider's stable lowercase code (e.g. <c>"parklio"</c>).</param>
/// <param name="Name">Human-readable name shown in the admin UI.</param>
/// <param name="OwnerType">
/// Optional owner type (e.g. <c>"Landlord"</c>, <c>"Partner"</c>).
/// <see langword="null"/> means platform-wide.
/// </param>
/// <param name="OwnerId">Optional owner identifier matching <paramref name="OwnerType"/>.</param>
/// <param name="Environment">Deployment environment: <c>"sandbox"</c> or <c>"production"</c>.</param>
/// <param name="Enabled">Whether this connection is active.</param>
/// <param name="Settings">Non-secret settings bound to the provider's <c>SettingsType</c>.</param>
/// <param name="SecretRef">Key into <see cref="ISecretStore"/>; never the secret itself.</param>
/// <param name="WebhookKey">Random URL-safe key used in the inbound webhook path.</param>
public sealed record IntegrationConnection(
    Guid Id,
    string ProviderCode,
    string Name,
    string? OwnerType,
    string? OwnerId,
    string Environment,
    bool Enabled,
    JsonElement Settings,
    string SecretRef,
    string WebhookKey);
