namespace Tot.Integrations;

/// <summary>
/// Describes a provider's capabilities, auth scheme, and settings contract.
/// Created once per provider type and registered at startup.
/// </summary>
/// <param name="Code">
/// Stable lowercase identifier used in URLs and configuration keys (e.g. <c>"parklio"</c>).
/// </param>
/// <param name="DisplayName">Human-readable name shown in the admin UI.</param>
/// <param name="Category">
/// Free-text category string (e.g. <c>"parking-hardware"</c>).
/// The package defines no fixed list.
/// </param>
/// <param name="SettingsType">
/// The type of the provider's non-secret settings class, validated with DataAnnotations.
/// </param>
/// <param name="Auth">The authentication scheme used by this provider.</param>
/// <param name="Capabilities">
/// Capability interface types implemented by this provider (e.g. <c>typeof(IAccessControl)</c>).
/// These interfaces are defined in the consuming system, not in this package.
/// </param>
/// <param name="SupportsWebhooks">Whether this provider can receive inbound webhooks.</param>
/// <param name="SupportsPolling">Whether this provider supports scheduled polling.</param>
public sealed record ProviderDescriptor(
    string Code,
    string DisplayName,
    string Category,
    Type SettingsType,
    AuthScheme Auth,
    IReadOnlyList<Type> Capabilities,
    bool SupportsWebhooks,
    bool SupportsPolling);
