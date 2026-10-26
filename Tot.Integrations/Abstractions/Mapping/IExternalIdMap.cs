namespace Tot.Integrations;

/// <summary>
/// Maps internal identifiers (e.g. <c>connector:CP-12/2</c>) to external provider identifiers
/// (e.g. <c>parking-place:8841</c>) and vice versa, scoped to a single connection.
/// </summary>
public interface IExternalIdMap
{
    /// <summary>
    /// Returns the external reference for an internal resource, or <see langword="null"/> if unmapped.
    /// </summary>
    Task<ExternalRef?> GetExternalAsync(string internalType, string internalId, string externalType, CancellationToken ct);

    /// <summary>
    /// Returns the internal id for an external reference, or <see langword="null"/> if unmapped.
    /// </summary>
    Task<(string InternalType, string InternalId)?> GetInternalAsync(ExternalRef externalRef, CancellationToken ct);

    /// <summary>
    /// Stores or updates a mapping. Throws if the external ref is already mapped to a different internal id.
    /// </summary>
    Task UpsertAsync(string internalType, string internalId, ExternalRef externalRef,
        System.Text.Json.JsonElement? meta = null, CancellationToken ct = default);

    /// <summary>Removes a mapping by internal resource reference.</summary>
    Task DeleteAsync(string internalType, string internalId, string externalType, CancellationToken ct);
}
