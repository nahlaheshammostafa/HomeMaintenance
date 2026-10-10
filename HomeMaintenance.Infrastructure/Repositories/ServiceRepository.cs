using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Enums;
using HomeMaintenance.Domain.Repositories;
using HomeMaintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace HomeMaintenance.Infrastructure.Repositories
{
    public class ServiceRepository : Repository<Service>, IServiceRepository
    {
        public ServiceRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        public async Task<IReadOnlyList<Service>> GetActiveServicesAsync(CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(s => s.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Service>> GetByTypeAsync(ServiceType type, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(s => s.Type == type && s.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasBookingsAsync(Guid serviceId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .AnyAsync(s => s.Id == serviceId && s.Bookings.Any(), cancellationToken);
        }
    }
}
