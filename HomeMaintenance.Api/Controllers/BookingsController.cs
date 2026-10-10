namespace HomeMaintenance.Api.Controllers;

using HomeMaintenance.Application.Bookings.Contracts;
using HomeMaintenance.Application.Bookings.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public class BookingsController : ApiController
{
    private readonly IBookingAppService _bookingAppService;

    public BookingsController(IBookingAppService bookingAppService)
    {
        _bookingAppService = bookingAppService;
    }

    // =========================================================================
    // 1. Queries
    // =========================================================================

    /// <summary>
    /// Retrieves all bookings across the platform.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.GetAllAsync(cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves a booking by its unique identifier, including assignments and status history.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.GetByIdAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves all bookings for a specific customer.
    /// </summary>
    [HttpGet("customer/{customerId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerBookings(Guid customerId, CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.GetCustomerBookingsAsync(customerId, cancellationToken);
        return HandleResult(result);
    }

    // =========================================================================
    // 2. Booking Creation & Automated Dispatch
    // =========================================================================

    /// <summary>
    /// Creates a new booking and automatically matches & dispatches an eligible technician.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBookingRequestDto input,
        CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.CreateBookingAsync(input, cancellationToken);
        return HandleCreatedResult(
            result,
            nameof(GetById),
            new { id = result.IsSuccess ? result.Value.Id : Guid.Empty });
    }

    // =========================================================================
    // 3. Technician Dispatch Responses
    // =========================================================================

    /// <summary>
    /// Technician accepts the assigned booking. Transitions status to Confirmed.
    /// </summary>
    [HttpPost("{id:guid}/assignments/{technicianId:guid}/accept")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptAssignment(
        Guid id,
        Guid technicianId,
        CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.AcceptAssignmentAsync(id, technicianId, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Technician declines the assigned booking. Triggers automated reassignment loop.
    /// </summary>
    [HttpPost("{id:guid}/assignments/{technicianId:guid}/reject")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectAssignment(
        Guid id,
        Guid technicianId,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.RejectAssignmentAsync(id, technicianId, reason, cancellationToken);
        return HandleResult(result);
    }

    // =========================================================================
    // 4. Job Execution
    // =========================================================================

    /// <summary>
    /// Technician starts the maintenance visit. Transitions status to InProgress.
    /// </summary>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartBooking(
        Guid id,
        [FromQuery] Guid technicianId,
        CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.StartBookingAsync(id, technicianId, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Technician completes the maintenance visit. Transitions status to Completed.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteBooking(
        Guid id,
        [FromQuery] Guid technicianId,
        CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.CompleteBookingAsync(id, technicianId, cancellationToken);
        return HandleResult(result);
    }

    // =========================================================================
    // 5. Cancellation & Rescheduling
    // =========================================================================

    /// <summary>
    /// Cancels a booking. Open to anyone (customer, technician, admin) as long as not completed.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelBookingRequestDto input,
        CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.CancelBookingAsync(id, input, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Reschedules a booking to a new time and re-evaluates technician availability.
    /// </summary>
    [HttpPost("{id:guid}/reschedule")]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reschedule(
        Guid id,
        [FromBody] RescheduleBookingRequestDto input,
        CancellationToken cancellationToken)
    {
        var result = await _bookingAppService.RescheduleBookingAsync(id, input, cancellationToken);
        return HandleResult(result);
    }
}
