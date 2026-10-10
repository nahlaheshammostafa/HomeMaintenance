namespace HomeMaintenance.Application.Services.Dtos;

using HomeMaintenance.Domain.Enums;

public class CreateUpdateServiceDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ServiceType Type { get; set; }
    public int DurationInMinutes { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
}
