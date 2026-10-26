using System.Data;
using System.Data.Common;

namespace Tot.Integrations;

/// <summary>
/// Enqueues integration commands into the outbox table.
/// Callers may join the enqueue to their own business transaction to guarantee
/// the command row is committed iff the business change is committed.
/// </summary>
public interface IIntegrationCommandQueue
{
    /// <summary>
    /// Inserts the command into the outbox (status <c>Pending</c>).
    /// If <paramref name="dbTransaction"/> is supplied the insert joins that transaction.
    /// </summary>
    /// <param name="command">The command to enqueue.</param>
    /// <param name="dbTransaction">Optional open transaction from the caller's DbContext.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The id of the newly created (or existing duplicate) command row.
    /// A duplicate <c>(connectionId, idempotencyKey)</c> returns the existing id without error.
    /// </returns>
    Task<Guid> EnqueueAsync(
        IntegrationCommand command,
        DbTransaction? dbTransaction = null,
        CancellationToken ct = default);
}
