namespace HomeMaintenance.Application.Bookings.Contracts;

using HomeMaintenance.Core.Results;
using HomeMaintenance.Domain.Entities;

public interface ITechnicianAssignmentEngine
{
    /// <summary>
    /// Searches for eligible technicians satisfying all availability constraints
    /// and selects one using the random selection strategy.
    /// </summary>
    Task<Result<Technician>> SelectTechnicianAsync(
        Guid serviceId,
        Guid serviceAreaId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? bookingId = null,
        CancellationToken cancellationToken = default);
}
