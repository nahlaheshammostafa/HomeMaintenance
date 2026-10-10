namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;

/// <summary>
/// Represents a service technician who provides maintenance services.
/// </summary>
public class Technician : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<TechnicianService> TechnicianServices { get; set; } = new List<TechnicianService>();
    public ICollection<TechnicianServiceArea> TechnicianServiceAreas { get; set; } = new List<TechnicianServiceArea>();
    public ICollection<WorkingHour> WorkingHours { get; set; } = new List<WorkingHour>();
    public ICollection<UnavailablePeriod> UnavailablePeriods { get; set; } = new List<UnavailablePeriod>();
    public ICollection<BookingAssignment> BookingAssignments { get; set; } = new List<BookingAssignment>();
}
