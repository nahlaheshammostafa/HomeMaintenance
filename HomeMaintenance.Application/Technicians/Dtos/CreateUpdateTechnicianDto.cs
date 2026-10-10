namespace HomeMaintenance.Application.Technicians.Dtos;

public class CreateUpdateTechnicianDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
