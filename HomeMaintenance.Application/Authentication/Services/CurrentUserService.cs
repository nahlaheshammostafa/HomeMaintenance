namespace HomeMaintenance.Application.Authentication.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HomeMaintenance.Application.Authentication.Contracts;
using Microsoft.AspNetCore.Http;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var idClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(idClaim, out var guid) ? guid : null;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email)
                           ?? User?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public string? FullName => User?.FindFirstValue(ClaimTypes.Name);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList()
        ?? (IReadOnlyList<string>)Array.Empty<string>();

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
