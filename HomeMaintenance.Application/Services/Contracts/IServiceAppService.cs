namespace HomeMaintenance.Application.Services.Contracts;

using HomeMaintenance.Application.Services.Dtos;
using HomeMaintenance.Core.Results;
using HomeMaintenance.Domain.Enums;

public interface IServiceAppService
{
    Task<Result<ServiceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceDto>>> GetActiveServicesAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ServiceDto>>> GetByTypeAsync(ServiceType type, CancellationToken cancellationToken = default);
    Task<Result<ServiceDto>> CreateAsync(CreateUpdateServiceDto input, CancellationToken cancellationToken = default);
    Task<Result<ServiceDto>> UpdateAsync(Guid id, CreateUpdateServiceDto input, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
