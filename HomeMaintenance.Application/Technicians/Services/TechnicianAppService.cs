namespace HomeMaintenance.Application.Technicians.Services;

using FluentValidation;
using HomeMaintenance.Application.Common.Extensions;
using HomeMaintenance.Application.Technicians.Contracts;
using HomeMaintenance.Application.Technicians.Dtos;
using HomeMaintenance.Application.Technicians.Mapping;
using HomeMaintenance.Core.Results;
using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Repositories;

public class TechnicianAppService : ITechnicianAppService
{
    private readonly ITechnicianRepository _technicianRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateUpdateTechnicianDto> _validator;
    private readonly IValidator<WorkingHourDto> _workingHourValidator;
    private readonly IValidator<AddUnavailablePeriodDto> _unavailablePeriodValidator;

    public TechnicianAppService(
        ITechnicianRepository technicianRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateUpdateTechnicianDto> validator,
        IValidator<WorkingHourDto> workingHourValidator,
        IValidator<AddUnavailablePeriodDto> unavailablePeriodValidator)
    {
        _technicianRepository = technicianRepository;
        _unitOfWork = unitOfWork;
        _validator = validator;
        _workingHourValidator = workingHourValidator;
        _unavailablePeriodValidator = unavailablePeriodValidator;
    }

    public async Task<Result<TechnicianDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var technician = await _technicianRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (technician is null)
            return Error.NotFound("Technician.NotFound", $"Technician with ID '{id}' was not found.");

        return technician.ToDto();
    }

    public async Task<Result<IReadOnlyList<TechnicianDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var technicians = await _technicianRepository.GetAllAsync(cancellationToken);
        return technicians.Select(t => t.ToDto()).ToList();
    }

    public async Task<Result<TechnicianDto>> CreateAsync(CreateUpdateTechnicianDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("Technician.ValidationFailed", "Validation failed.", validationResult.ToDictionary());

        var technician = input.ToEntity();
        await _technicianRepository.AddAsync(technician, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return technician.ToDto();
    }

    public async Task<Result<TechnicianDto>> UpdateAsync(Guid id, CreateUpdateTechnicianDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("Technician.ValidationFailed", "Validation failed.", validationResult.ToDictionary());

        var technician = await _technicianRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (technician is null)
            return Error.NotFound("Technician.NotFound", $"Technician with ID '{id}' was not found.");

        input.MapTo(technician);
        _technicianRepository.Update(technician);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return technician.ToDto();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var technician = await _technicianRepository.GetByIdAsync(id, cancellationToken);
        if (technician is null)
            return Error.NotFound("Technician.NotFound", $"Technician with ID '{id}' was not found.");

        _technicianRepository.Delete(technician);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<TechnicianDto>> AssignServicesAsync(Guid id, List<Guid> serviceIds, CancellationToken cancellationToken = default)
    {
        var technician = await _technicianRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (technician is null)
            return Error.NotFound("Technician.NotFound", $"Technician with ID '{id}' was not found.");

        var desiredServiceIds = serviceIds.Distinct().ToHashSet();

        // 1. Remove qualifications no longer selected
        var toRemove = technician.TechnicianServices
            .Where(ts => !desiredServiceIds.Contains(ts.ServiceId))
            .ToList();

        foreach (var item in toRemove)
        {
            technician.TechnicianServices.Remove(item);
        }

        // 2. Add only newly selected qualifications
        var existingServiceIds = technician.TechnicianServices
            .Select(ts => ts.ServiceId)
            .ToHashSet();

        foreach (var serviceId in desiredServiceIds.Where(sId => !existingServiceIds.Contains(sId)))
        {
            technician.TechnicianServices.Add(new TechnicianService
            {
                TechnicianId = id,
                ServiceId = serviceId,
                IsActive = true
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _technicianRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        return updated!.ToDto();
    }

    public async Task<Result<TechnicianDto>> AssignAreasAsync(Guid id, List<Guid> areaIds, CancellationToken cancellationToken = default)
    {
        var technician = await _technicianRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (technician is null)
            return Error.NotFound("Technician.NotFound", $"Technician with ID '{id}' was not found.");

        var desiredAreaIds = areaIds.Distinct().ToHashSet();

        // 1. Remove areas no longer selected
        var toRemove = technician.TechnicianServiceAreas
            .Where(tsa => !desiredAreaIds.Contains(tsa.ServiceAreaId))
            .ToList();

        foreach (var item in toRemove)
        {
            technician.TechnicianServiceAreas.Remove(item);
        }

        // 2. Add only newly selected areas
        var existingAreaIds = technician.TechnicianServiceAreas
            .Select(tsa => tsa.ServiceAreaId)
            .ToHashSet();

        foreach (var areaId in desiredAreaIds.Where(aId => !existingAreaIds.Contains(aId)))
        {
            technician.TechnicianServiceAreas.Add(new TechnicianServiceArea
            {
                TechnicianId = id,
                ServiceAreaId = areaId,
                IsActive = true
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _technicianRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        return updated!.ToDto();
    }

    public async Task<Result<TechnicianDto>> SetWorkingHoursAsync(Guid id, List<WorkingHourDto> workingHours, CancellationToken cancellationToken = default)
    {
        // 1. Validate each shift
        foreach (var shift in workingHours)
        {
            var validationResult = await _workingHourValidator.ValidateAsync(shift, cancellationToken);
            if (!validationResult.IsValid)
                return Error.Validation("WorkingHour.Invalid", "Invalid shift boundaries.", validationResult.ToDictionary());
        }

        var technician = await _technicianRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (technician is null)
            return Error.NotFound("Technician.NotFound", $"Technician with ID '{id}' was not found.");

        // 2. Replace working shifts
        technician.WorkingHours.Clear();
        foreach (var shift in workingHours)
        {
            technician.WorkingHours.Add(new WorkingHour
            {
                TechnicianId = id,
                DayOfWeek = shift.DayOfWeek,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return technician.ToDto();
    }

    public async Task<Result<TechnicianDto>> AddUnavailablePeriodAsync(Guid id, AddUnavailablePeriodDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await _unavailablePeriodValidator.ValidateAsync(input, cancellationToken);
        if (!validationResult.IsValid)
            return Error.Validation("UnavailablePeriod.Invalid", "Invalid time off.", validationResult.ToDictionary());

        var technician = await _technicianRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (technician is null)
            return Error.NotFound("Technician.NotFound", $"Technician with ID '{id}' was not found.");

        // Rule: Non-overlapping unavailable periods
        //bool hasOverlap = technician.UnavailablePeriods.Any(up =>
        //    up.StartTimeUtc < input.EndTimeUtc && up.EndTimeUtc > input.StartTimeUtc);

        //if (hasOverlap)
        //    return Error.Conflict("UnavailablePeriod.Overlap", "The technician already has an unavailable period overlapping these times.");

        technician.UnavailablePeriods.Add(new UnavailablePeriod
        {
            TechnicianId = id,
            StartTimeUtc = input.StartTimeUtc,
            EndTimeUtc = input.EndTimeUtc,
            Reason = input.Reason
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return technician.ToDto();
    }
}
