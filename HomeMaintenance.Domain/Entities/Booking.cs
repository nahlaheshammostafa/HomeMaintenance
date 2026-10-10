namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;
using HomeMaintenance.Domain.Enums;

/// <summary>
/// Central entity representing a maintenance service appointment.
/// Pure domain model holding booking data, associations, and audit state.
/// </summary>
public class Booking : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    /// <summary>
    /// Frozen snapshot of where the service is to be performed.
    /// Preserved historically even if customer's default address updates.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    public string? CancellationReason { get; set; }

    // Navigation properties
    public ICollection<BookingAssignment> BookingAssignments { get; set; } = new List<BookingAssignment>();
    public ICollection<BookingStatusHistory> BookingStatusHistories { get; set; } = new List<BookingStatusHistory>();
}
