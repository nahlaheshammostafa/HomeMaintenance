using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HomeMaintenance.Domain.Repositories
{
    public interface IServiceAreaRepository : IRepository<ServiceArea>
    {
        Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ServiceArea>> GetAreasByServiceIdAsync(Guid serviceId, CancellationToken cancellationToken = default);
    }
}
