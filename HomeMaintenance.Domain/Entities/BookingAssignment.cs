namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;
using HomeMaintenance.Domain.Enums;

/// <summary>
/// Tracks the dispatch of a specific technician to a booking.
/// Pure data entity holding assignment status and response timestamps.
/// </summary>
public class BookingAssignment : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAtUtc { get; set; }
}
