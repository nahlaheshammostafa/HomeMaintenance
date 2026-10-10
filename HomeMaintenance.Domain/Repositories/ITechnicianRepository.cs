namespace HomeMaintenance.Domain.Repositories;

using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Entities;

public interface ITechnicianRepository : IRepository<Technician>
{
    Task<Technician?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Technician>> GetEligibleTechniciansAsync(
        Guid serviceId,
        Guid serviceAreaId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateTime startUtc,
        DateTime endUtc,
        Guid? bookingId = null,
        CancellationToken cancellationToken = default);
}