namespace HomeMaintenance.Api.Controllers;

using HomeMaintenance.Application.Services.Contracts;
using HomeMaintenance.Application.Services.Dtos;
using HomeMaintenance.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public class ServicesController : ApiController
{
    private readonly IServiceAppService _serviceAppService;

    public ServicesController(IServiceAppService serviceAppService)
    {
        _serviceAppService = serviceAppService;
    }

    /// <summary>
    /// Retrieves services with optional filtering.
    /// </summary>
    /// <param name="onlyActive">If true, returns only active services for customer booking.</param>
    /// <param name="type">Optional filter by service category (e.g., Plumbing, Electrical).</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool onlyActive = false,
        [FromQuery] ServiceType? type = null,
        CancellationToken cancellationToken = default)
    {
        if (type.HasValue)
        {
            var byTypeResult = await _serviceAppService.GetByTypeAsync(type.Value, cancellationToken);
            return HandleResult(byTypeResult);
        }

        if (onlyActive)
        {
            var activeResult = await _serviceAppService.GetActiveServicesAsync(cancellationToken);
            return HandleResult(activeResult);
        }

        var result = await _serviceAppService.GetAllAsync(cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves a service by its unique identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serviceAppService.GetByIdAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Creates a new maintenance service.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUpdateServiceDto input,
        CancellationToken cancellationToken)
    {
        var result = await _serviceAppService.CreateAsync(input, cancellationToken);
        return HandleCreatedResult(
            result,
            nameof(GetById),
            new { id = result.IsSuccess ? result.Value.Id : Guid.Empty });
    }

    /// <summary>
    /// Updates an existing service's details, duration, or base price.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CreateUpdateServiceDto input,
        CancellationToken cancellationToken)
    {
        var result = await _serviceAppService.UpdateAsync(id, input, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Deactivates or removes a service by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serviceAppService.DeleteAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
