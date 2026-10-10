using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Repositories;
using HomeMaintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HomeMaintenance.Infrastructure.Repositories
{
    public class ServiceAreaRepository : Repository<ServiceArea>, IServiceAreaRepository
    {
        public ServiceAreaRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        public async Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default)
        {
            return await DbSet.AnyAsync(sa => sa.Name.ToLower() == name.ToLower(), cancellationToken);
        }
    }
}
