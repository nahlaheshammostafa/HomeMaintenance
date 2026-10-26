namespace HomeMaintenance.Api.Controllers;

using HomeMaintenance.Application.Authentication.Contracts;
using HomeMaintenance.Application.Authentication.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
public class AuthController : ApiController
{
    private readonly IAuthAppService _authAppService;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(
        IAuthAppService authAppService,
        ICurrentUserService currentUserService)
    {
        _authAppService = authAppService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Registers a new user account and returns an authentication token.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _authAppService.RegisterAsync(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Authenticates a user with email and password and returns a JWT token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _authAppService.LoginAsync(request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Returns the currently authenticated user's profile claims.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        return Ok(new
        {
            userId = _currentUserService.UserId,
            email = _currentUserService.Email,
            fullName = _currentUserService.FullName,
            roles = _currentUserService.Roles
        });
    }
}
