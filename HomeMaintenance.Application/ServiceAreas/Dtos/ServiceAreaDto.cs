namespace HomeMaintenance.Application.ServiceAreas.Dtos;

public class ServiceAreaDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
