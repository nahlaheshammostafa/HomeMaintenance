namespace Tot.Integrations;

/// <summary>
/// Thrown by <see cref="IConnectionStore.UpdateAsync"/> when a concurrent update modifies
/// the same connection row between the caller's read and write.
/// </summary>
public sealed class ConnectionConcurrencyException : Exception
{
    /// <summary>The id of the connection that caused the conflict.</summary>
    public Guid ConnectionId { get; }

    /// <inheritdoc/>
    public ConnectionConcurrencyException(Guid connectionId)
        : base($"A concurrent update was detected for connection '{connectionId}'. Re-read the connection and retry.")
    {
        ConnectionId = connectionId;
    }

    /// <inheritdoc/>
    public ConnectionConcurrencyException(Guid connectionId, Exception inner)
        : base($"A concurrent update was detected for connection '{connectionId}'. Re-read the connection and retry.", inner)
    {
        ConnectionId = connectionId;
    }
}
