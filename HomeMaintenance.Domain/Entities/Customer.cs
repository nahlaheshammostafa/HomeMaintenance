namespace HomeMaintenance.Domain.Entities;

using HomeMaintenance.Core.Common;

/// <summary>
/// Represents a customer registered in the system.
/// </summary>
public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string DefaultAddress { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
