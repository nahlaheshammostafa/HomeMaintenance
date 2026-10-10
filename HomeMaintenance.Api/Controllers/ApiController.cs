namespace HomeMaintenance.Api.Controllers;

using HomeMaintenance.Core.Results;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiController : ControllerBase
{
    /// <summary>
    /// Handles a non-generic Result (operations without return payload like Delete or Cancel).
    /// </summary>
    protected IActionResult HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return HandleError(result.Error);
    }

    /// <summary>
    /// Handles a Result<T> producing 200 OK with data, or the appropriate HTTP error.
    /// </summary>
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return HandleError(result.Error);
    }

    /// <summary>
    /// Handles a Result<T> for creation actions, producing 201 Created with route header.
    /// </summary>
    protected IActionResult HandleCreatedResult<T>(
        Result<T> result,
        string actionName,
        object routeValues)
    {
        if (result.IsSuccess)
        {
            return CreatedAtAction(actionName, routeValues, result.Value);
        }

        return HandleError(result.Error);
    }

    /// <summary>
    /// Centralized mapping of domain ErrorType to standard HTTP status codes.
    /// Returns standardized RFC 7807 ProblemDetails for clients.
    /// </summary>
    private IActionResult HandleError(Error error)
    {
        return error.Type switch
        {
            ErrorType.Validation => HandleValidationError(error),

            ErrorType.NotFound => NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = error.Code,
                Detail = error.Description
            }),

            ErrorType.Conflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = error.Code,
                Detail = error.Description
            }),

            ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = error.Code,
                Detail = error.Description
            }),

            ErrorType.Unauthorized => Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = error.Code,
                Detail = error.Description
            }),

            _ => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = error.Code,
                Detail = error.Description
            })
        };
    }

    private IActionResult HandleValidationError(Error error)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();

        if (error.ValidationErrors is not null)
        {
            foreach (var (property, errors) in error.ValidationErrors)
            {
                foreach (var err in errors)
                {
                    modelState.AddModelError(property, err);
                }
            }
        }

        return ValidationProblem(
            modelStateDictionary: modelState,
            title: error.Code,
            detail: error.Description,
            statusCode: StatusCodes.Status400BadRequest);
    }
}
