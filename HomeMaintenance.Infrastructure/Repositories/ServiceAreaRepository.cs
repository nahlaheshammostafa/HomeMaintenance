using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Repositories;
using HomeMaintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public async Task<IReadOnlyList<ServiceArea>> GetAreasByServiceIdAsync(Guid serviceId, CancellationToken cancellationToken = default)
        {
            return await DbContext.TechnicianServiceAreas
                .Where(tsa => tsa.IsActive &&
                              tsa.Technician.IsActive &&
                              tsa.Technician.TechnicianServices.Any(ts => ts.ServiceId == serviceId && ts.IsActive))
                .Select(tsa => tsa.ServiceArea)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
    }
}
