namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;

/// <summary>
/// Represents a geographic region or zone served by technicians.
/// </summary>
public class ServiceArea : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<TechnicianServiceArea> TechnicianServiceAreas { get; set; } = new List<TechnicianServiceArea>();
}
