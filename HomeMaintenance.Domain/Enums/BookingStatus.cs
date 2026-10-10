namespace HomeMaintenance.Domain.Enums;

/// <summary>
/// Represents the distinct stages of a booking lifecycle.
/// Strict valid transitions:
/// Pending -> Confirmed -> InProgress -> Completed
/// Cancellation allowed: Pending -> Cancelled, Confirmed -> Cancelled (if >= 24h prior)
/// </summary>
public enum BookingStatus
{
    Pending = 1,
    Confirmed = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}
