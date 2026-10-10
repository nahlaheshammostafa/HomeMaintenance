namespace HomeMaintenance.Domain.Repositories;

using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Entities;

public interface IBookingRepository : IRepository<Booking>
{
    Task<Booking?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetCustomerBookingsAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Booking>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);
    Task<bool> HasOverlappingBookingAsync(
        Guid technicianId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default);
}