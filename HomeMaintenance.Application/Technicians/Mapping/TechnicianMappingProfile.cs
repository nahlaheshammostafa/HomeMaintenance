namespace HomeMaintenance.Application.Technicians.Mapping;

using HomeMaintenance.Application.Technicians.Dtos;
using HomeMaintenance.Domain.Entities;

public static class TechnicianMappingProfile
{
    public static TechnicianDto ToDto(this Technician entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Email = entity.Email,
            PhoneNumber = entity.PhoneNumber,
            IsActive = entity.IsActive,
            CreatedAtUtc = entity.CreatedAtUtc,
            QualifiedServices = entity.TechnicianServices
                .Select(ts => new TechnicianServiceSummaryDto(ts.ServiceId, ts.Service?.Name ?? string.Empty, ts.IsActive))
                .ToList(),
            ServiceAreas = entity.TechnicianServiceAreas
                .Select(tsa => new TechnicianAreaSummaryDto(tsa.ServiceAreaId, tsa.ServiceArea?.Name ?? string.Empty, tsa.IsActive))
                .ToList(),
            WorkingHours = entity.WorkingHours
                .Select(wh => new WorkingHourDto { DayOfWeek = wh.DayOfWeek, StartTime = wh.StartTime, EndTime = wh.EndTime })
                .ToList(),
            UnavailablePeriods = entity.UnavailablePeriods
                .Select(up => new UnavailablePeriodDto { Id = up.Id, StartTimeUtc = up.StartTimeUtc, EndTimeUtc = up.EndTimeUtc, Reason = up.Reason })
                .ToList()
        };

    public static Technician ToEntity(this CreateUpdateTechnicianDto dto) =>
        new()
        {
            Name = dto.Name,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            IsActive = dto.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

    public static void MapTo(this CreateUpdateTechnicianDto dto, Technician entity)
    {
        entity.Name = dto.Name;
        entity.Email = dto.Email;
        entity.PhoneNumber = dto.PhoneNumber;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }
}
