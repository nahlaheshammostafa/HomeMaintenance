namespace HomeMaintenance.Domain.Entities.Identity;

using Microsoft.AspNetCore.Identity;

/// <summary>
/// Represents a user identity in the system.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
