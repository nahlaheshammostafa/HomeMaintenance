namespace HomeMaintenance.Application.Authentication.Contracts;

/// <summary>
/// Provides access to the authenticated user's contextual identity information.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? FullName { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
}
