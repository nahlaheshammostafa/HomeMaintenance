using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace HomeMaintenance.Domain.Repositories
{
    public interface IServiceAreaRepository : IRepository<ServiceArea>
    {
        Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default); 
    }
}
