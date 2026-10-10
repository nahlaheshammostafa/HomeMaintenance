namespace HomeMaintenance.Application.Customers.Services;

using FluentValidation;
using HomeMaintenance.Application.Customers.Contracts;
using HomeMaintenance.Application.Customers.Dtos;
using HomeMaintenance.Application.Customers.Mapping;
using HomeMaintenance.Core.Results;
using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Repositories;

public class CustomerAppService : ICustomerAppService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateUpdateCustomerDto> _validator;

    public CustomerAppService(
        ICustomerRepository customerRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateUpdateCustomerDto> validator)
    {
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<CustomerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer is null)
            return Error.NotFound("Customer.NotFound", $"Customer with ID '{id}' was not found.");

        return customer.ToDto();
    }

    public async Task<Result<IReadOnlyList<CustomerDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var customers = await _customerRepository.GetAllAsync(cancellationToken);
        var dtos = customers.Select(c => c.ToDto()).ToList();
        return dtos;
    }

    public async Task<Result<CustomerDto>> CreateAsync(CreateUpdateCustomerDto input, CancellationToken cancellationToken = default)
    {
        // 1. Validate Input
        var validationResult = await _validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            return Error.Validation("Customer.ValidationFailed", "Validation failed.", errors);
        }

        // 2. Business Rule: Unique Email
        if (await _customerRepository.EmailExistsAsync(input.Email, cancellationToken))
            return Error.Conflict("Customer.DuplicateEmail", $"A customer with email '{input.Email}' already exists.");

        // 3. Map & Save
        var customer = input.ToEntity();
        await _customerRepository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.ToDto();
    }

    public async Task<Result<CustomerDto>> UpdateAsync(Guid id, CreateUpdateCustomerDto input, CancellationToken cancellationToken = default)
    {
        // 1. Validate Input
        var validationResult = await _validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            return Error.Validation("Customer.ValidationFailed", "Validation failed.", errors);
        }

        // 2. Fetch existing customer
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer is null)
            return Error.NotFound("Customer.NotFound", $"Customer with ID '{id}' was not found.");

        // 3. Business Rule: Unique Email (if email was changed)
        if (!string.Equals(customer.Email, input.Email, StringComparison.OrdinalIgnoreCase) &&
            await _customerRepository.EmailExistsAsync(input.Email, cancellationToken))
        {
            return Error.Conflict("Customer.DuplicateEmail", $"Email '{input.Email}' is already in use by another customer.");
        }

        // 4. Map & Update
        input.MapTo(customer);
        _customerRepository.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return customer.ToDto();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);
        if (customer is null)
            return Error.NotFound("Customer.NotFound", $"Customer with ID '{id}' was not found.");

        _customerRepository.Delete(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}