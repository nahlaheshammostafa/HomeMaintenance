namespace HomeMaintenance.Api.Controllers;

using HomeMaintenance.Application.Technicians.Contracts;
using HomeMaintenance.Application.Technicians.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public class TechniciansController : ApiController
{
    private readonly ITechnicianAppService _technicianAppService;

    public TechniciansController(ITechnicianAppService technicianAppService)
    {
        _technicianAppService = technicianAppService;
    }

    // =========================================================================
    // 1. Profile CRUD
    // =========================================================================

    /// <summary>
    /// Retrieves all technicians.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TechnicianDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _technicianAppService.GetAllAsync(cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves a technician by their ID with their qualifications, shifts, and leaves.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TechnicianDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _technicianAppService.GetByIdAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Registers a new technician.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TechnicianDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUpdateTechnicianDto input,
        CancellationToken cancellationToken)
    {
        var result = await _technicianAppService.CreateAsync(input, cancellationToken);
        return HandleCreatedResult(
            result,
            nameof(GetById),
            new { id = result.IsSuccess ? result.Value.Id : Guid.Empty });
    }

    /// <summary>
    /// Updates a technician's basic profile.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TechnicianDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CreateUpdateTechnicianDto input,
        CancellationToken cancellationToken)
    {
        var result = await _technicianAppService.UpdateAsync(id, input, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Deactivates/removes a technician.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _technicianAppService.DeleteAsync(id, cancellationToken);
        return HandleResult(result);
    }

    // =========================================================================
    // 2. Qualifications & Service Area Coverage
    // =========================================================================

    /// <summary>
    /// Assigns the services that this technician is qualified to perform.
    /// </summary>
    [HttpPost("{id:guid}/services")]
    [ProducesResponseType(typeof(TechnicianDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignServices(
        Guid id,
        [FromBody] List<Guid> serviceIds,
        CancellationToken cancellationToken)
    {
        var result = await _technicianAppService.AssignServicesAsync(id, serviceIds, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Assigns the coverage areas that this technician serves.
    /// </summary>
    [HttpPost("{id:guid}/areas")]
    [ProducesResponseType(typeof(TechnicianDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignAreas(
        Guid id,
        [FromBody] List<Guid> areaIds,
        CancellationToken cancellationToken)
    {
        var result = await _technicianAppService.AssignAreasAsync(id, areaIds, cancellationToken);
        return HandleResult(result);
    }

    // =========================================================================
    // 3. Shifts & Leaves
    // =========================================================================

    /// <summary>
    /// Sets the recurring weekly shifts for this technician.
    /// </summary>
    [HttpPost("{id:guid}/working-hours")]
    [ProducesResponseType(typeof(TechnicianDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetWorkingHours(
        Guid id,
        [FromBody] List<WorkingHourDto> workingHours,
        CancellationToken cancellationToken)
    {
        var result = await _technicianAppService.SetWorkingHoursAsync(id, workingHours, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Adds a leave or unavailable time period (e.g., vacation, sick leave).
    /// </summary>
    [HttpPost("{id:guid}/unavailable-periods")]
    [ProducesResponseType(typeof(TechnicianDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddUnavailablePeriod(
        Guid id,
        [FromBody] AddUnavailablePeriodDto input,
        CancellationToken cancellationToken)
    {
        var result = await _technicianAppService.AddUnavailablePeriodAsync(id, input, cancellationToken);
        return HandleResult(result);
    }
}
