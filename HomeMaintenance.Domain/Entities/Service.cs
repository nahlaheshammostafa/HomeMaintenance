namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;
using HomeMaintenance.Domain.Enums;

/// <summary>
/// Represents a maintenance service offered by the platform.
/// </summary>
public class Service : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ServiceType Type { get; set; }

    /// <summary>
    /// Service duration in minutes. Used to compute booking EndTime (EndTime = StartTime + Duration).
    /// </summary>
    public int DurationInMinutes { get; set; }

    /// <summary>
    /// Base price in currency units. Must be >= 0.
    /// </summary>
    public decimal BasePrice { get; set; }

    /// <summary>
    /// Whether the service is active and available for customer bookings.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<TechnicianService> TechnicianServices { get; set; } = new List<TechnicianService>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
