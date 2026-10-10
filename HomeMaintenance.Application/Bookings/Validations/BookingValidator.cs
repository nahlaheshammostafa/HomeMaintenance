namespace HomeMaintenance.Application.Bookings.Validations;

using FluentValidation;
using HomeMaintenance.Application.Bookings.Dtos;

public class CreateBookingValidator : AbstractValidator<CreateBookingRequestDto>
{
    public CreateBookingValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required.");

        RuleFor(x => x.ServiceId)
            .NotEmpty().WithMessage("Service ID is required.");

        RuleFor(x => x.ServiceAreaId)
            .NotEmpty().WithMessage("Service Area ID is required.");

        RuleFor(x => x.StartTimeUtc)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Booking start time must be in the future.");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Service location is required.")
            .MaximumLength(500).WithMessage("Location cannot exceed 500 characters.");
    }
}

public class CancelBookingValidator : AbstractValidator<CancelBookingRequestDto>
{
    public CancelBookingValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Cancellation reason is required.")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}

public class RescheduleBookingValidator : AbstractValidator<RescheduleBookingRequestDto>
{
    public RescheduleBookingValidator()
    {
        RuleFor(x => x.ServiceAreaId)
            .NotEmpty().WithMessage("Service Area ID is required.");

        RuleFor(x => x.NewStartTimeUtc)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("New booking start time must be in the future.");
    }
}
