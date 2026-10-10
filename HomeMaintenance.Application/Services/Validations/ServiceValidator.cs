namespace HomeMaintenance.Application.Services.Validations;

using FluentValidation;
using HomeMaintenance.Application.Services.Dtos;

public class ServiceValidator : AbstractValidator<CreateUpdateServiceDto>
{
    public ServiceValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Service name is required.")
            .MaximumLength(150).WithMessage("Service name cannot exceed 150 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("A valid service type must be specified.");

        RuleFor(x => x.DurationInMinutes)
            .GreaterThan(0).WithMessage("Duration must be greater than 0 minutes.");

        RuleFor(x => x.BasePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Base price cannot be negative.");
    }
}
