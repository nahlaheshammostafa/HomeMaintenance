namespace HomeMaintenance.Api.Controllers;

using HomeMaintenance.Application.ServiceAreas.Contracts;
using HomeMaintenance.Application.ServiceAreas.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[Route("api/service-areas")]
public class ServiceAreasController : ApiController
{
    private readonly IServiceAreaAppService _serviceAreaAppService;

    public ServiceAreasController(IServiceAreaAppService serviceAreaAppService)
    {
        _serviceAreaAppService = serviceAreaAppService;
    }

    /// <summary>
    /// Retrieves all service coverage areas (e.g., Cairo, Giza, Alexandria).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceAreaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _serviceAreaAppService.GetAllAsync(cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Retrieves a service area by its unique identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceAreaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serviceAreaAppService.GetByIdAsync(id, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Creates a new service coverage area.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceAreaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUpdateServiceAreaDto input,
        CancellationToken cancellationToken)
    {
        var result = await _serviceAreaAppService.CreateAsync(input, cancellationToken);
        return HandleCreatedResult(
            result,
            nameof(GetById),
            new { id = result.IsSuccess ? result.Value.Id : Guid.Empty });
    }

    /// <summary>
    /// Updates an existing service area's name.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ServiceAreaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CreateUpdateServiceAreaDto input,
        CancellationToken cancellationToken)
    {
        var result = await _serviceAreaAppService.UpdateAsync(id, input, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Deletes a service area by its ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serviceAreaAppService.DeleteAsync(id, cancellationToken);
        return HandleResult(result);
    }
}
