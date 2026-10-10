namespace HomeMaintenance.Application.Technicians.Dtos;

public class TechnicianDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Aggregate Details
    public List<TechnicianServiceSummaryDto> QualifiedServices { get; set; } = new();
    public List<TechnicianAreaSummaryDto> ServiceAreas { get; set; } = new();
    public List<WorkingHourDto> WorkingHours { get; set; } = new();
    public List<UnavailablePeriodDto> UnavailablePeriods { get; set; } = new();
}

public record TechnicianServiceSummaryDto(Guid ServiceId, string ServiceName, bool IsActive);
public record TechnicianAreaSummaryDto(Guid ServiceAreaId, string AreaName, bool IsActive);
