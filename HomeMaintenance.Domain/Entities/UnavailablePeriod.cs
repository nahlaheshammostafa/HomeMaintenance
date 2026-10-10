namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;

/// <summary>
/// Represents a time window when a technician is marked unavailable (e.g., leave, sickness, maintenance).
/// Unavailable periods must not overlap for the same technician.
/// </summary>
public class UnavailablePeriod : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public string? Reason { get; set; }
}
