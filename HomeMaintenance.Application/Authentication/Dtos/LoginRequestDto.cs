namespace HomeMaintenance.Application.Authentication.Dtos;

/// <summary>
/// Data transfer object for user login.
/// </summary>
public record LoginRequestDto(
    string Email,
    string Password
);
