namespace Tot.Integrations;

/// <summary>Persistent store for <see cref="IntegrationConnection"/> records.</summary>
public interface IConnectionStore
{
    /// <summary>Returns the connection with the given id, or <see langword="null"/>.</summary>
    Task<IntegrationConnection?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Returns the connection whose <c>WebhookKey</c> matches, or <see langword="null"/>.</summary>
    Task<IntegrationConnection?> GetByWebhookKeyAsync(string webhookKey, CancellationToken ct);

    /// <summary>Lists connections optionally filtered by provider and/or owner.</summary>
    Task<IReadOnlyList<IntegrationConnection>> ListAsync(
        string? providerCode = null,
        string? ownerType = null,
        string? ownerId = null,
        CancellationToken ct = default);

    /// <summary>Persists a new connection and returns it with any server-generated fields populated.</summary>
    Task<IntegrationConnection> CreateAsync(IntegrationConnection connection, CancellationToken ct);

    /// <summary>Updates an existing connection.</summary>
    /// <exception cref="ConnectionConcurrencyException">
    /// Thrown when a concurrent update modified the row between the caller's read and this write.
    /// Re-read the connection and retry.
    /// </exception>
    Task<IntegrationConnection> UpdateAsync(IntegrationConnection connection, CancellationToken ct);

    /// <summary>Permanently deletes a connection by id.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
