namespace HomeMaintenance.Application.Bookings.Dtos;

using HomeMaintenance.Domain.Enums;

public class BookingStatusHistoryDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string? Reason { get; set; }
}
