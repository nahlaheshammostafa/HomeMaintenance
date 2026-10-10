namespace HomeMaintenance.Application.ServiceAreas.Services;

using FluentValidation;
using HomeMaintenance.Application.Common.Extensions;
using HomeMaintenance.Application.ServiceAreas.Contracts;
using HomeMaintenance.Application.ServiceAreas.Dtos;
using HomeMaintenance.Application.ServiceAreas.Mapping;
using HomeMaintenance.Core.Results;
using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Repositories;

public class ServiceAreaAppService : IServiceAreaAppService
{
    private readonly IServiceAreaRepository _serviceAreaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateUpdateServiceAreaDto> _validator;

    public ServiceAreaAppService(
        IServiceAreaRepository serviceAreaRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateUpdateServiceAreaDto> validator)
    {
        _serviceAreaRepository = serviceAreaRepository;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<Result<ServiceAreaDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var area = await _serviceAreaRepository.GetByIdAsync(id, cancellationToken);
        if (area is null)
            return Error.NotFound("ServiceArea.NotFound", $"Service area with ID '{id}' was not found.");

        return area.ToDto();
    }

    public async Task<Result<IReadOnlyList<ServiceAreaDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var areas = await _serviceAreaRepository.GetAllAsync(cancellationToken);
        return areas.Select(a => a.ToDto()).ToList();
    }

    public async Task<Result<ServiceAreaDto>> CreateAsync(CreateUpdateServiceAreaDto input, CancellationToken cancellationToken = default)
    {
        // 1. Validate
        var validationResult = await _validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("ServiceArea.ValidationFailed", "Validation failed.", validationResult.ToDictionary());

        // 2. Business Rule: Unique area name
        if (await _serviceAreaRepository.NameExistsAsync(input.Name, cancellationToken))
            return Error.Conflict("ServiceArea.DuplicateName", $"A service area with name '{input.Name}' already exists.");

        // 3. Map & Save
        var area = input.ToEntity();
        await _serviceAreaRepository.AddAsync(area, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return area.ToDto();
    }

    public async Task<Result<ServiceAreaDto>> UpdateAsync(Guid id, CreateUpdateServiceAreaDto input, CancellationToken cancellationToken = default)
    {
        // 1. Validate
        var validationResult = await _validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("ServiceArea.ValidationFailed", "Validation failed.", validationResult.ToDictionary());

        // 2. Fetch existing
        var area = await _serviceAreaRepository.GetByIdAsync(id, cancellationToken);
        if (area is null)
            return Error.NotFound("ServiceArea.NotFound", $"Service area with ID '{id}' was not found.");

        // 3. Business Rule: Unique name (if changed)
        if (!string.Equals(area.Name, input.Name, StringComparison.OrdinalIgnoreCase) &&
            await _serviceAreaRepository.NameExistsAsync(input.Name, cancellationToken))
        {
            return Error.Conflict("ServiceArea.DuplicateName", $"A service area with name '{input.Name}' already exists.");
        }

        // 4. Map & Update
        input.MapTo(area);
        _serviceAreaRepository.Update(area);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return area.ToDto();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var area = await _serviceAreaRepository.GetByIdAsync(id, cancellationToken);
        if (area is null)
            return Error.NotFound("ServiceArea.NotFound", $"Service area with ID '{id}' was not found.");

        _serviceAreaRepository.Delete(area);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
