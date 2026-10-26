namespace HomeMaintenance.Application.Authentication.Dtos;

/// <summary>
/// Data transfer object for user registration.
/// </summary>
public record RegisterRequestDto(
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber = null,
    string? Role = null
);
