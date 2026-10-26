namespace HomeMaintenance.Application.Authentication.Contracts;

using HomeMaintenance.Application.Authentication.Dtos;
using HomeMaintenance.Core.Results;

/// <summary>
/// Application service handling user registration and login workflows.
/// </summary>
public interface IAuthAppService
{
    Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
