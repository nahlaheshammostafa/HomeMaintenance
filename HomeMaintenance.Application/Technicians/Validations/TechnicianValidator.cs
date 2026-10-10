namespace HomeMaintenance.Application.Technicians.Validations;

using FluentValidation;
using HomeMaintenance.Application.Technicians.Dtos;

public class TechnicianValidator : AbstractValidator<CreateUpdateTechnicianDto>
{
    public TechnicianValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name cannot exceed 150 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is invalid.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^\+?\d{2,15}$").WithMessage("Phone number is invalid.");
    }
}

public class WorkingHourValidator : AbstractValidator<WorkingHourDto>
{
    public WorkingHourValidator()
    {
        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .WithMessage("Shift end time must be after start time.");
    }
}

public class UnavailablePeriodValidator : AbstractValidator<AddUnavailablePeriodDto>
{
    public UnavailablePeriodValidator()
    {
        RuleFor(x => x.EndTimeUtc)
            .GreaterThan(x => x.StartTimeUtc)
            .WithMessage("Unavailable period end time must be after start time.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}
