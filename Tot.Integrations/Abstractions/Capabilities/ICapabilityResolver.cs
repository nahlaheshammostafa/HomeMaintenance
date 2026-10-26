namespace Tot.Integrations;

/// <summary>
/// Resolves a capability interface implementation for a specific connection.
/// The capability interfaces themselves (e.g. <c>IAccessControl</c>) are defined in the consuming system.
/// </summary>
public interface ICapabilityResolver
{
    /// <summary>
    /// Returns the implementation of <typeparamref name="TCapability"/> bound to the given connection,
    /// or throws <see cref="InvalidOperationException"/> if the connection is disabled or
    /// the capability is not registered for its provider.
    /// </summary>
    /// <typeparam name="TCapability">The capability interface type.</typeparam>
    /// <param name="connectionId">The connection to resolve for.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TCapability> GetAsync<TCapability>(Guid connectionId, CancellationToken ct)
        where TCapability : class;

    /// <summary>
    /// Returns all enabled connections that support <typeparamref name="TCapability"/>
    /// and are owned by the given owner, along with their capability instances.
    /// </summary>
    Task<IReadOnlyList<(IntegrationConnection Connection, TCapability Capability)>> GetAllAsync<TCapability>(
        string? ownerType,
        string? ownerId,
        CancellationToken ct)
        where TCapability : class;
}
