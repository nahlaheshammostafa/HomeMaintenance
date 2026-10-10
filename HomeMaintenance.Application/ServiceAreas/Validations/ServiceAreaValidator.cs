namespace HomeMaintenance.Application.ServiceAreas.Validations;

using FluentValidation;
using HomeMaintenance.Application.ServiceAreas.Dtos;

public class ServiceAreaValidator : AbstractValidator<CreateUpdateServiceAreaDto>
{
    public ServiceAreaValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Service area name is required.")
            .MaximumLength(150).WithMessage("Service area name cannot exceed 150 characters.");
    }
}
