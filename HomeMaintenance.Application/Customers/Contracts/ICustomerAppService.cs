namespace HomeMaintenance.Application.Customers.Contracts;

using HomeMaintenance.Application.Customers.Dtos;
using HomeMaintenance.Core.Results;

public interface ICustomerAppService
{
    Task<Result<CustomerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<CustomerDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<CustomerDto>> CreateAsync(CreateUpdateCustomerDto input, CancellationToken cancellationToken = default);
    Task<Result<CustomerDto>> UpdateAsync(Guid id, CreateUpdateCustomerDto input, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}