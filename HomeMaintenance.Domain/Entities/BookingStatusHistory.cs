namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;
using HomeMaintenance.Domain.Enums;

/// <summary>
/// Append-only audit trail record capturing every important status transition of a booking.
/// Pure data entity.
/// </summary>
public class BookingStatusHistory : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public BookingStatus Status { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}
