namespace HomeMaintenance.Application.Technicians.Contracts;

using HomeMaintenance.Application.Technicians.Dtos;
using HomeMaintenance.Core.Results;

public interface ITechnicianAppService
{
    // Profile CRUD
    Task<Result<TechnicianDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TechnicianDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<TechnicianDto>> CreateAsync(CreateUpdateTechnicianDto input, CancellationToken cancellationToken = default);
    Task<Result<TechnicianDto>> UpdateAsync(Guid id, CreateUpdateTechnicianDto input, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Qualifications & Coverage Management
    Task<Result<TechnicianDto>> AssignServicesAsync(Guid id, List<Guid> serviceIds, CancellationToken cancellationToken = default);
    Task<Result<TechnicianDto>> AssignAreasAsync(Guid id, List<Guid> areaIds, CancellationToken cancellationToken = default);

    // Shifts & Leaves
    Task<Result<TechnicianDto>> SetWorkingHoursAsync(Guid id, List<WorkingHourDto> workingHours, CancellationToken cancellationToken = default);
    Task<Result<TechnicianDto>> AddUnavailablePeriodAsync(Guid id, AddUnavailablePeriodDto input, CancellationToken cancellationToken = default);
}
