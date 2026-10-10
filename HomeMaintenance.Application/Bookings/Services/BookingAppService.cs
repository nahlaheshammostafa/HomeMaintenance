namespace HomeMaintenance.Application.Bookings.Services;

using FluentValidation;
using HomeMaintenance.Application.Bookings.Contracts;
using HomeMaintenance.Application.Bookings.Dtos;
using HomeMaintenance.Application.Bookings.Mapping;
using HomeMaintenance.Application.Common.Extensions;
using HomeMaintenance.Core.Results;
using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Enums;
using HomeMaintenance.Domain.Repositories;

public class BookingAppService : IBookingAppService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IServiceAreaRepository _serviceAreaRepository;
    private readonly ITechnicianAssignmentEngine _assignmentEngine;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateBookingRequestDto> _createValidator;
    private readonly IValidator<CancelBookingRequestDto> _cancelValidator;
    private readonly IValidator<RescheduleBookingRequestDto> _rescheduleValidator;

    public BookingAppService(
        IBookingRepository bookingRepository,
        IServiceRepository serviceRepository,
        ICustomerRepository customerRepository,
        IServiceAreaRepository serviceAreaRepository,
        ITechnicianAssignmentEngine assignmentEngine,
        IUnitOfWork unitOfWork,
        IValidator<CreateBookingRequestDto> createValidator,
        IValidator<CancelBookingRequestDto> cancelValidator,
        IValidator<RescheduleBookingRequestDto> rescheduleValidator)
    {
        _bookingRepository = bookingRepository;
        _serviceRepository = serviceRepository;
        _customerRepository = customerRepository;
        _serviceAreaRepository = serviceAreaRepository;
        _assignmentEngine = assignmentEngine;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _cancelValidator = cancelValidator;
        _rescheduleValidator = rescheduleValidator;
    }

    public async Task<Result<BookingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (booking is null)
            return Error.NotFound("Booking.NotFound", $"Booking with ID '{id}' was not found.");

        return booking.ToDto();
    }

    public async Task<Result<IReadOnlyList<BookingDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.GetAllWithDetailsAsync(cancellationToken);
        return bookings.Select(b => b.ToDto()).ToList();
    }

    public async Task<Result<IReadOnlyList<BookingDto>>> GetCustomerBookingsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.GetCustomerBookingsAsync(customerId, cancellationToken);
        return bookings.Select(b => b.ToDto()).ToList();
    }

    // =========================================================================
    // 1. Create Booking
    // =========================================================================
    public async Task<Result<BookingDto>> CreateBookingAsync(CreateBookingRequestDto input, CancellationToken cancellationToken = default)
    {
        // 1. Validate Input
        var validationResult = await _createValidator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("Booking.ValidationFailed", "Validation failed.", validationResult.ToDictionary());

        // 2. Validate Customer exists
        var customer = await _customerRepository.GetByIdAsync(input.CustomerId, cancellationToken);
        if (customer is null)
            return Error.NotFound("Customer.NotFound", "Customer does not exist.");

        // 3. Validate Service is active & calculate EndTime
        var service = await _serviceRepository.GetByIdAsync(input.ServiceId, cancellationToken);
        if (service is null || !service.IsActive)
            return Error.NotFound("Service.InactiveOrNotFound", "Selected service is unavailable.");

        // 4. Validate ServiceArea exists
        var serviceArea = await _serviceAreaRepository.GetByIdAsync(input.ServiceAreaId, cancellationToken);
        if (serviceArea is null)
            return Error.NotFound("ServiceArea.NotFound", "Selected service area does not exist.");

        var endTimeUtc = input.StartTimeUtc.AddMinutes(service.DurationInMinutes);

        // 5. Find eligible technician (Random strategy)
        var assignmentResult = await _assignmentEngine.SelectTechnicianAsync(
            input.ServiceId,
            input.ServiceAreaId,
            input.StartTimeUtc,
            endTimeUtc,
            null,
            cancellationToken);

        if (assignmentResult.IsFailure)
            return assignmentResult.Error;

        var technician = assignmentResult.Value;

        // 6. Create Booking, Initial Assignment, and Audit History
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = input.CustomerId,
            ServiceId = input.ServiceId,
            StartTimeUtc = input.StartTimeUtc,
            EndTimeUtc = endTimeUtc,
            Status = BookingStatus.Pending,
            Location = input.Location
        };

        booking.BookingAssignments.Add(new BookingAssignment
        {
            Booking = booking,
            TechnicianId = technician.Id,
            Status = AssignmentStatus.Pending,
            AssignedAtUtc = DateTime.UtcNow
        });

        booking.BookingStatusHistories.Add(new BookingStatusHistory
        {
            Booking = booking,
            Status = BookingStatus.Pending,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = $"Booking created. Dispatched to technician '{technician.Name}'."
        });

        await _bookingRepository.AddAsync(booking, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(booking.Id, cancellationToken);
    }

    // =========================================================================
    // 2. Technician Accepts
    // =========================================================================
    public async Task<Result<BookingDto>> AcceptAssignmentAsync(Guid bookingId, Guid technicianId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId, cancellationToken);
        if (booking is null)
            return Error.NotFound("Booking.NotFound", "Booking not found.");

        if (booking.Status != BookingStatus.Pending)
            return Error.Conflict("Booking.InvalidStatus", $"Only pending bookings can be confirmed. Current status: '{booking.Status}'.");

        var assignment = booking.BookingAssignments.FirstOrDefault(ba => ba.TechnicianId == technicianId && ba.Status == AssignmentStatus.Pending);
        if (assignment is null)
            return Error.NotFound("Assignment.NotFound", "No pending assignment found for this technician on this booking.");

        // Overlap safety check before confirming
        if (await _bookingRepository.HasOverlappingBookingAsync(technicianId, booking.StartTimeUtc, booking.EndTimeUtc, booking.Id, cancellationToken))
            return Error.Conflict("Booking.Overlap", "Technician is no longer available at this time.");

        // Update Assignment
        assignment.Status = AssignmentStatus.Accepted;
        assignment.RespondedAtUtc = DateTime.UtcNow;
        assignment.UpdatedAtUtc = DateTime.UtcNow;

        // Update Booking
        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAtUtc = DateTime.UtcNow;

        // Audit Trail
        booking.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            Status = BookingStatus.Confirmed,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = "Technician accepted assignment."
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return booking.ToDto();
    }

    // =========================================================================
    // 3. Technician Rejects & Reassignment Loop
    // =========================================================================
    public async Task<Result<BookingDto>> RejectAssignmentAsync(Guid bookingId, Guid technicianId, string? rejectionReason = null, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId, cancellationToken);
        if (booking is null)
            return Error.NotFound("Booking.NotFound", "Booking not found.");

        if (booking.Status != BookingStatus.Pending)
            return Error.Conflict("Booking.InvalidStatus", "Cannot reject an assignment for a non-pending booking.");

        var assignment = booking.BookingAssignments.FirstOrDefault(ba => ba.TechnicianId == technicianId && ba.Status == AssignmentStatus.Pending);
        if (assignment is null)
            return Error.NotFound("Assignment.NotFound", "No pending assignment found for this technician.");

        // 1. Mark current assignment as Rejected
        assignment.Status = AssignmentStatus.Rejected;
        assignment.RespondedAtUtc = DateTime.UtcNow;
        assignment.UpdatedAtUtc = DateTime.UtcNow;

        booking.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            Status = BookingStatus.Pending,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = $"Technician rejected assignment. Reason: {rejectionReason ?? "None provided"}."
        });

        // 2. Identify service area
        var serviceAreaId = assignment.Technician?.TechnicianServiceAreas.FirstOrDefault()?.ServiceAreaId ?? Guid.Empty;

        // 3. Search for the NEXT eligible technician (excluding technicians who already rejected this booking)
        var nextTechResult = await _assignmentEngine.SelectTechnicianAsync(
            booking.ServiceId,
            serviceAreaId,
            booking.StartTimeUtc,
            booking.EndTimeUtc,
            booking.Id, // Guarantees excluded technicians cannot be picked again!
            cancellationToken);

        if (nextTechResult.IsSuccess)
        {
            var nextTech = nextTechResult.Value;

            booking.BookingAssignments.Add(new BookingAssignment
            {
                BookingId = booking.Id,
                TechnicianId = nextTech.Id,
                Status = AssignmentStatus.Pending,
                AssignedAtUtc = DateTime.UtcNow
            });

            booking.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                Status = BookingStatus.Pending,
                ChangedAtUtc = DateTime.UtcNow,
                Reason = $"Technician rejected. Reassigned to '{nextTech.Name}'."
            });
        }
        else
        {
            // Case 03: All technicians rejected -> Cancel booking automatically
            booking.Status = BookingStatus.Cancelled;
            booking.CancellationReason = "NoTechnicianAvailable";
            booking.UpdatedAtUtc = DateTime.UtcNow;

            booking.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingId = booking.Id,
                Status = BookingStatus.Cancelled,
                ChangedAtUtc = DateTime.UtcNow,
                Reason = "Booking cancelled automatically: all eligible technicians rejected or none available."
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return booking.ToDto();
    }

    // =========================================================================
    // 4. Start Booking
    // =========================================================================
    public async Task<Result<BookingDto>> StartBookingAsync(Guid bookingId, Guid technicianId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId, cancellationToken);
        if (booking is null)
            return Error.NotFound("Booking.NotFound", "Booking not found.");

        if (booking.Status != BookingStatus.Confirmed)
            return Error.Conflict("Booking.InvalidStatus", $"Only Confirmed bookings can move to InProgress. Current status: '{booking.Status}'.");

        var acceptedAssignment = booking.BookingAssignments.FirstOrDefault(ba => ba.TechnicianId == technicianId && ba.Status == AssignmentStatus.Accepted);
        if (acceptedAssignment is null)
            return Error.Forbidden("Technician.NotAssigned", "You are not the confirmed technician for this booking.");

        booking.Status = BookingStatus.InProgress;
        booking.UpdatedAtUtc = DateTime.UtcNow;

        booking.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            Status = BookingStatus.InProgress,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = "Technician started service."
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return booking.ToDto();
    }

    // =========================================================================
    // 5. Complete Booking
    // =========================================================================
    public async Task<Result<BookingDto>> CompleteBookingAsync(Guid bookingId, Guid technicianId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId, cancellationToken);
        if (booking is null)
            return Error.NotFound("Booking.NotFound", "Booking not found.");

        if (booking.Status != BookingStatus.InProgress)
            return Error.Conflict("Booking.InvalidStatus", $"Only InProgress bookings can be marked Completed. Current status: '{booking.Status}'.");

        var acceptedAssignment = booking.BookingAssignments.FirstOrDefault(ba => ba.TechnicianId == technicianId && ba.Status == AssignmentStatus.Accepted);
        if (acceptedAssignment is null)
            return Error.Forbidden("Technician.NotAssigned", "You are not the confirmed technician for this booking.");

        booking.Status = BookingStatus.Completed;
        booking.UpdatedAtUtc = DateTime.UtcNow;

        booking.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            Status = BookingStatus.Completed,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = "Technician completed service."
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return booking.ToDto();
    }

    // =========================================================================
    // 6. Cancel Booking (Anyone can cancel, no admin check)
    // =========================================================================
    public async Task<Result<BookingDto>> CancelBookingAsync(Guid bookingId, CancelBookingRequestDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await _cancelValidator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("Booking.ValidationFailed", "Validation failed.", validationResult.ToDictionary());

        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId, cancellationToken);
        if (booking is null)
            return Error.NotFound("Booking.NotFound", "Booking not found.");

        if (booking.Status == BookingStatus.Completed)
            return Error.Conflict("Booking.Completed", "A completed booking cannot be cancelled.");

        if (booking.Status == BookingStatus.Cancelled)
            return Error.Conflict("Booking.AlreadyCancelled", "The booking is already cancelled.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancellationReason = input.Reason;
        booking.UpdatedAtUtc = DateTime.UtcNow;

        booking.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            Status = BookingStatus.Cancelled,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = $"Booking cancelled. Reason: {input.Reason}."
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return booking.ToDto();
    }

    // =========================================================================
    // 7. Reschedule Booking
    // =========================================================================
    public async Task<Result<BookingDto>> RescheduleBookingAsync(Guid bookingId, RescheduleBookingRequestDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await _rescheduleValidator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("Booking.ValidationFailed", "Validation failed.", validationResult.ToDictionary());

        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId, cancellationToken);
        if (booking is null)
            return Error.NotFound("Booking.NotFound", "Booking not found.");

        if (booking.Status == BookingStatus.Completed || booking.Status == BookingStatus.Cancelled || booking.Status == BookingStatus.InProgress)
            return Error.Conflict("Booking.CannotReschedule", $"Cannot reschedule a booking with status '{booking.Status}'.");

        var service = await _serviceRepository.GetByIdAsync(booking.ServiceId, cancellationToken);
        if (service is null)
            return Error.NotFound("Service.NotFound", "Service not found.");

        var newEndTimeUtc = input.NewStartTimeUtc.AddMinutes(service.DurationInMinutes);

        // Check availability for the new time
        var assignmentResult = await _assignmentEngine.SelectTechnicianAsync(
            booking.ServiceId,
            input.ServiceAreaId,
            input.NewStartTimeUtc,
            newEndTimeUtc,
            booking.Id,
            cancellationToken);

        if (assignmentResult.IsFailure)
            return assignmentResult.Error;

        var technician = assignmentResult.Value;

        // Update schedule & create fresh assignment
        booking.StartTimeUtc = input.NewStartTimeUtc;
        booking.EndTimeUtc = newEndTimeUtc;
        booking.Status = BookingStatus.Pending; // Moves back to pending awaiting the new tech's confirmation
        booking.UpdatedAtUtc = DateTime.UtcNow;

        booking.BookingAssignments.Add(new BookingAssignment
        {
            BookingId = booking.Id,
            TechnicianId = technician.Id,
            Status = AssignmentStatus.Pending,
            AssignedAtUtc = DateTime.UtcNow
        });

        booking.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            Status = BookingStatus.Pending,
            ChangedAtUtc = DateTime.UtcNow,
            Reason = $"Rescheduled to {input.NewStartTimeUtc:u}. Reassigned to '{technician.Name}'."
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return booking.ToDto();
    }
}
