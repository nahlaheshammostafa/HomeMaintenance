namespace HomeMaintenance.Application.Customers.Mapping;

using HomeMaintenance.Application.Customers.Dtos;
using HomeMaintenance.Domain.Entities;

public static class CustomerMappingProfile
{
    public static CustomerDto ToDto(this Customer entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Email = entity.Email,
            PhoneNumber = entity.PhoneNumber,
            DefaultAddress = entity.DefaultAddress,
            CreatedAtUtc = entity.CreatedAtUtc
        };

    public static Customer ToEntity(this CreateUpdateCustomerDto dto) =>
        new()
        {
            Name = dto.Name,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            DefaultAddress = dto.DefaultAddress,
            CreatedAtUtc = DateTime.UtcNow
        };

    public static void MapTo(this CreateUpdateCustomerDto dto, Customer entity)
    {
        entity.Name = dto.Name;
        entity.Email = dto.Email;
        entity.PhoneNumber = dto.PhoneNumber;
        entity.DefaultAddress = dto.DefaultAddress;
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }
}