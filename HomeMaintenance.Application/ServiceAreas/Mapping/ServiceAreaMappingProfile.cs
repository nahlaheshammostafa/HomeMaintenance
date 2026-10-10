namespace HomeMaintenance.Application.ServiceAreas.Mapping;

using HomeMaintenance.Application.ServiceAreas.Dtos;
using HomeMaintenance.Domain.Entities;

public static class ServiceAreaMappingProfile
{
    public static ServiceAreaDto ToDto(this ServiceArea entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            CreatedAtUtc = entity.CreatedAtUtc
        };

    public static ServiceArea ToEntity(this CreateUpdateServiceAreaDto dto) =>
        new()
        {
            Name = dto.Name,
            CreatedAtUtc = DateTime.UtcNow
        };

    public static void MapTo(this CreateUpdateServiceAreaDto dto, ServiceArea entity)
    {
        entity.Name = dto.Name;
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }
}
