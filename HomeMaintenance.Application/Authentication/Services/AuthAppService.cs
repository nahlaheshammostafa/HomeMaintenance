namespace HomeMaintenance.Application.Authentication.Services;

using FluentValidation;
using HomeMaintenance.Application.Authentication.Contracts;
using HomeMaintenance.Application.Authentication.Dtos;
using HomeMaintenance.Core.Results;
using HomeMaintenance.Domain.Constants;
using HomeMaintenance.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;

public class AuthAppService : IAuthAppService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;

    public AuthAppService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    public async Task<Result<AuthResponseDto>> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate Input
        var validationResult = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            return Error.Validation("Auth.ValidationFailed", "Validation failed.", errors);
        }

        // 2. Ensure Email is Unique
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Error.Conflict("Auth.EmailExists", $"User with email '{request.Email}' already exists.");
        }

        // 3. Resolve and Ensure Role
        var roleToAssign = string.IsNullOrWhiteSpace(request.Role) ? AppRoles.Customer : request.Role;
        if (!await _roleManager.RoleExistsAsync(roleToAssign))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(roleToAssign));
        }

        // 4. Create User
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return Error.Validation("Auth.RegistrationFailed", "User registration failed.", errors);
        }

        // 5. Assign Role
        var roleResult = await _userManager.AddToRoleAsync(user, roleToAssign);
        if (!roleResult.Succeeded)
        {
            var errors = roleResult.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return Error.Validation("Auth.RoleAssignmentFailed", "Failed to assign role.", errors);
        }

        // 6. Generate Token & Return Response
        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user, roles);

        return new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email!,
            roles.ToList(),
            token,
            expiresAt
        );
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate Input
        var validationResult = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            return Error.Validation("Auth.ValidationFailed", "Validation failed.", errors);
        }

        // 2. Verify Credentials
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");
        }

        // 3. Verify Account Status
        if (!user.IsActive)
        {
            return Error.Forbidden("Auth.UserDeactivated", "This account has been deactivated.");
        }

        // 4. Generate Token & Return Response
        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user, roles);

        return new AuthResponseDto(
            user.Id,
            user.FullName,
            user.Email!,
            roles.ToList(),
            token,
            expiresAt
        );
    }
}
