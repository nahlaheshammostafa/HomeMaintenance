namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;

/// <summary>
/// Represents a regular working shift for a technician on a specific day of the week.
/// A technician can have multiple shifts per day (e.g., 09:00-13:00 and 14:00-18:00).
/// </summary>
public class WorkingHour : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
