namespace HomeMaintenance.Application.Authentication.Validators;

using FluentValidation;
using HomeMaintenance.Application.Authentication.Dtos;
using HomeMaintenance.Domain.Constants;

public class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(200).WithMessage("Email cannot exceed 200 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters long.");

        When(x => !string.IsNullOrWhiteSpace(x.Role), () =>
        {
            RuleFor(x => x.Role!)
                .Must(role => AppRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
                .WithMessage($"Role must be one of: {string.Join(", ", AppRoles.All)}.");
        });
    }
}
