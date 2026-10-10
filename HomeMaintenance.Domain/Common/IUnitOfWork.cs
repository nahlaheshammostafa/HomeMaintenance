namespace HomeMaintenance.Domain.Common;

/// <summary>
/// Unit of work interface managing atomic persistence and explicit database transactions.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all changes tracked by the DbContext within a single atomic operation.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit database transaction (used for pessimistic concurrency locks).
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the active database transaction.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the active database transaction.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
