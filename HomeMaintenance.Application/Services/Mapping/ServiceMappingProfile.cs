namespace HomeMaintenance.Application.Services.Mapping;

using HomeMaintenance.Application.Services.Dtos;
using HomeMaintenance.Domain.Entities;

public static class ServiceMappingProfile
{
    public static ServiceDto ToDto(this Service entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Type = entity.Type,
            DurationInMinutes = entity.DurationInMinutes,
            BasePrice = entity.BasePrice,
            IsActive = entity.IsActive,
            CreatedAtUtc = entity.CreatedAtUtc
        };

    public static Service ToEntity(this CreateUpdateServiceDto dto) =>
        new()
        {
            Name = dto.Name,
            Description = dto.Description,
            Type = dto.Type,
            DurationInMinutes = dto.DurationInMinutes,
            BasePrice = dto.BasePrice,
            IsActive = dto.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

    public static void MapTo(this CreateUpdateServiceDto dto, Service entity)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Type = dto.Type;
        entity.DurationInMinutes = dto.DurationInMinutes;
        entity.BasePrice = dto.BasePrice;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }
}
