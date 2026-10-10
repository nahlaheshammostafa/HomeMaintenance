namespace HomeMaintenance.Application.Bookings.Contracts;

using HomeMaintenance.Application.Bookings.Dtos;
using HomeMaintenance.Core.Results;

public interface IBookingAppService
{
    Task<Result<BookingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BookingDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BookingDto>>> GetCustomerBookingsAsync(Guid customerId, CancellationToken cancellationToken = default);

    // Lifecycle Workflows
    Task<Result<BookingDto>> CreateBookingAsync(CreateBookingRequestDto input, CancellationToken cancellationToken = default);
    Task<Result<BookingDto>> AcceptAssignmentAsync(Guid bookingId, Guid technicianId, CancellationToken cancellationToken = default);
    Task<Result<BookingDto>> RejectAssignmentAsync(Guid bookingId, Guid technicianId, string? rejectionReason = null, CancellationToken cancellationToken = default);
    Task<Result<BookingDto>> StartBookingAsync(Guid bookingId, Guid technicianId, CancellationToken cancellationToken = default);
    Task<Result<BookingDto>> CompleteBookingAsync(Guid bookingId, Guid technicianId, CancellationToken cancellationToken = default);
    Task<Result<BookingDto>> CancelBookingAsync(Guid bookingId, CancelBookingRequestDto input, CancellationToken cancellationToken = default);
    Task<Result<BookingDto>> RescheduleBookingAsync(Guid bookingId, RescheduleBookingRequestDto input, CancellationToken cancellationToken = default);
}
