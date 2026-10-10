namespace HomeMaintenance.Application.Customers.Dtos;

public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string DefaultAddress { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}