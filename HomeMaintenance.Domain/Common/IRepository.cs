namespace HomeMaintenance.Domain.Common;

using HomeMaintenance.Core.Common;

/// <summary>
/// Generic repository interface for entities deriving from BaseEntity.
/// Encapsulates query and in-memory change tracking operations without committing changes.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void Delete(T entity);
}
