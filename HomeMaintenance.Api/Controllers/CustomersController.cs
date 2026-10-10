using HomeMaintenance.Application.Customers.Contracts;
using HomeMaintenance.Application.Customers.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HomeMaintenance.Api.Controllers
{
    public class CustomersController : ApiController
    {
        private readonly ICustomerAppService _customerAppService;
        public CustomersController(ICustomerAppService customerAppService)
        {
            _customerAppService = customerAppService;
        }

        /// <summary>
        /// Retrieves all registered customers.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
        {
            var result = await _customerAppService.GetAllAsync(cancellationToken);
            return HandleResult(result);
        }

        /// <summary>
        /// Retrieves a customer by their ID.
        /// </summary>
        /// <param name="id">The ID of the customer to retrieve.</param>
        /// <returns>The customer if found, otherwise a 404 Not Found response.</returns>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var result = await _customerAppService.GetByIdAsync(id, cancellationToken);
            return HandleResult(result);
        }

        /// <summary>
        /// Creates a new customer.
        /// </summary>
        /// <param name="customerDto">The customer data to create.</param>
        /// <returns>The created customer.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]

        public async Task<IActionResult> CreateAsync([FromBody] CreateUpdateCustomerDto input, CancellationToken cancellationToken)
        {
            var result = await _customerAppService.CreateAsync(input, cancellationToken);
            return HandleCreatedResult(
                result,
                nameof(GetById),
                new { id = result.IsSuccess ? result.Value.Id : Guid.Empty });
        }

        /// <summary>
        /// Updates an existing customer.
        /// </summary>
        /// <param name="id">The ID of the customer to update.</param>
        /// <param name="customerDto">The updated customer data.</param>
        /// <returns>The updated customer.</returns>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]

        public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] CreateUpdateCustomerDto input, CancellationToken cancellationToken)
        {
            var result = await _customerAppService.UpdateAsync(id, input, cancellationToken);
            return HandleResult(result);
        }

        /// <summary>
        /// Deletes a customer by their ID.
        /// </summary>
        /// <param name="id">The ID of the customer to delete.</param>
        /// <returns>A 200 OK response if successful, or a 404 Not Found response if not found.</returns>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var result = await _customerAppService.DeleteAsync(id, cancellationToken);
            return HandleResult(result);
        }
    }
}
