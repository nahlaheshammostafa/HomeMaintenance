namespace HomeMaintenance.Application.Services.Services;

using FluentValidation;
using HomeMaintenance.Application.Common.Extensions;
using HomeMaintenance.Application.Services.Contracts;
using HomeMaintenance.Application.Services.Dtos;
using HomeMaintenance.Application.Services.Mapping;
using HomeMaintenance.Core.Results;
using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Enums;
using HomeMaintenance.Domain.Repositories;

public class ServiceAppService : IServiceAppService
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateUpdateServiceDto> _validator;

    public ServiceAppService(
        IServiceRepository serviceRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateUpdateServiceDto> validator)
    {
        _serviceRepository = serviceRepository;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<ServiceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdAsync(id, cancellationToken);
        if (service is null)
            return Error.NotFound("Service.NotFound", $"Service with ID '{id}' was not found.");

        return service.ToDto();
    }

    public async Task<Result<IReadOnlyList<ServiceDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var services = await _serviceRepository.GetAllAsync(cancellationToken);
        return services.Select(s => s.ToDto()).ToList();
    }

    public async Task<Result<IReadOnlyList<ServiceDto>>> GetActiveServicesAsync(CancellationToken cancellationToken = default)
    {
        var services = await _serviceRepository.GetActiveServicesAsync(cancellationToken);
        return services.Select(s => s.ToDto()).ToList();
    }

    public async Task<Result<IReadOnlyList<ServiceDto>>> GetByTypeAsync(ServiceType type, CancellationToken cancellationToken = default)
    {
        var services = await _serviceRepository.GetByTypeAsync(type, cancellationToken);
        return services.Select(s => s.ToDto()).ToList();
    }

    public async Task<Result<ServiceDto>> CreateAsync(CreateUpdateServiceDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("Service.ValidationFailed", "Validation failed.", validationResult.ToDictionary());

        var service = input.ToEntity();
        await _serviceRepository.AddAsync(service, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return service.ToDto();
    }

    public async Task<Result<ServiceDto>> UpdateAsync(Guid id, CreateUpdateServiceDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("Service.ValidationFailed", "Validation failed.", validationResult.ToDictionary());

        var service = await _serviceRepository.GetByIdAsync(id, cancellationToken);
        if (service is null)
            return Error.NotFound("Service.NotFound", $"Service with ID '{id}' was not found.");

        input.MapTo(service);
        _serviceRepository.Update(service);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return service.ToDto();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetByIdAsync(id, cancellationToken);
        if (service is null)
            return Error.NotFound("Service.NotFound", $"Service with ID '{id}' was not found.");

        // Rule 11 from requirements: A service cannot be deleted if referenced by existing bookings
        if (await _serviceRepository.HasBookingsAsync(id, cancellationToken))
        {
            return Error.Conflict(
                "Service.HasHistoricalBookings",
                "This service cannot be deleted because it is referenced by existing bookings. Deactivate it instead by setting IsActive to false.");
        }

        _serviceRepository.Delete(service);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
