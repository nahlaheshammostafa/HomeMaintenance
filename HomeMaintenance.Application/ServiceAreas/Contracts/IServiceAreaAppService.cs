namespace HomeMaintenance.Application.ServiceAreas.Contracts;

using HomeMaintenance.Application.ServiceAreas.Dtos;
using HomeMaintenance.Core.Results;

public interface IServiceAreaAppService
{
    Task<Result<ServiceAreaDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceAreaDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<ServiceAreaDto>> CreateAsync(CreateUpdateServiceAreaDto input, CancellationToken cancellationToken = default);
    Task<Result<ServiceAreaDto>> UpdateAsync(Guid id, CreateUpdateServiceAreaDto input, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
