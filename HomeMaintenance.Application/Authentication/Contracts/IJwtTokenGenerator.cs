namespace HomeMaintenance.Application.Authentication.Contracts;

using HomeMaintenance.Domain.Entities.Identity;

/// <summary>
/// Service responsible for generating JSON Web Tokens for authenticated users.
/// </summary>
public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(ApplicationUser user, IEnumerable<string> roles);
}
