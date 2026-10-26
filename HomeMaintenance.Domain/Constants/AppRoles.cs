namespace HomeMaintenance.Domain.Constants;

/// <summary>
/// Defines application role names used for authorization throughout the system.
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Technician = "Technician";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> All = new[] { Admin, Technician, Customer };
}
