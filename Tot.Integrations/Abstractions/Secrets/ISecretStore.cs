namespace Tot.Integrations;

/// <summary>
/// Stores and retrieves encrypted secrets for integration connections.
/// Secrets are never returned by public APIs; only the runtime reads them.
/// </summary>
public interface ISecretStore
{
    /// <summary>Stores or updates the secret identified by <paramref name="secretRef"/>.</summary>
    /// <param name="secretRef">Stable key (e.g. <c>connection:{id}</c>).</param>
    /// <param name="value">The plaintext secret — stored encrypted at rest.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetAsync(string secretRef, string value, CancellationToken ct);

    /// <summary>Retrieves the decrypted secret for the runtime's use.</summary>
    /// <param name="secretRef">Stable key returned by <see cref="SetAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plaintext value, or <see langword="null"/> if not found.</returns>
    Task<string?> GetAsync(string secretRef, CancellationToken ct);

    /// <summary>
    /// Re-encrypts the secret with the current key ring without changing the plaintext value.
    /// Call periodically or when rotating the Data Protection key ring.
    /// </summary>
    Task RotateAsync(string secretRef, CancellationToken ct);

    /// <summary>Permanently removes a secret. Called when a connection is deleted.</summary>
    Task DeleteAsync(string secretRef, CancellationToken ct);
}
