namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;

/// <summary>
/// Represents a junction between Technician and Service indicating qualification to perform the service.
/// Enforces business uniqueness: a technician cannot be mapped to the same service twice.
/// </summary>
public class TechnicianService : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    /// <summary>
    /// Indicates if the technician is currently authorized/willing to receive this service.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
