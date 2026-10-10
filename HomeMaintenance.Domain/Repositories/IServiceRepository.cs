namespace HomeMaintenance.Domain.Repositories;

using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Enums;

public interface IServiceRepository : IRepository<Service>
{
    Task<IReadOnlyList<Service>> GetActiveServicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Service>> GetByTypeAsync(ServiceType type, CancellationToken cancellationToken = default);
    Task<bool> HasBookingsAsync(Guid serviceId, CancellationToken cancellationToken = default);
}