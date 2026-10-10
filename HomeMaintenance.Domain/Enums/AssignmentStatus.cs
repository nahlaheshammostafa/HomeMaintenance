namespace HomeMaintenance.Domain.Enums;

/// <summary>
/// Represents the response state of a technician dispatch for a specific booking.
/// </summary>
public enum AssignmentStatus
{
    Pending = 1,    // Dispatched to technician; awaiting their response
    Accepted = 2,   // Technician accepted; triggers Booking to Confirmed
    Rejected = 3    // Technician declined; triggers system to search for the next eligible technician
}
