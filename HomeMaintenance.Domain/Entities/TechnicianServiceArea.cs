namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;

/// <summary>
/// Represents a junction between Technician and ServiceArea indicating coverage.
/// Enforces business uniqueness: a technician cannot be mapped to the same area twice.
/// </summary>
public class TechnicianServiceArea : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public Guid ServiceAreaId { get; set; }
    public ServiceArea ServiceArea { get; set; } = null!;

    /// <summary>
    /// Indicates if the technician is currently active in this service area.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
