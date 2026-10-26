namespace HomeMaintenance.Application.Authentication.Dtos;

/// <summary>
/// Data transfer object returned upon successful authentication.
/// </summary>
public record AuthResponseDto(
    Guid Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    string Token,
    DateTime ExpiresAtUtc
);
