namespace HomeMaintenance.Application.Bookings.Services;

using HomeMaintenance.Application.Bookings.Contracts;
using HomeMaintenance.Core.Results;
using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Repositories;

public class TechnicianAssignmentEngine : ITechnicianAssignmentEngine
{
    private readonly ITechnicianRepository _technicianRepository;

    public TechnicianAssignmentEngine(ITechnicianRepository technicianRepository)
    {
        _technicianRepository = technicianRepository;
    }

    public async Task<Result<Technician>> SelectTechnicianAsync(
        Guid serviceId,
        Guid serviceAreaId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? bookingId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Convert booking window to operating local time for recurring shift evaluation
        var operatingTimeZone = GetOperatingTimeZone();
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(startUtc, operatingTimeZone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(endUtc, operatingTimeZone);

        var dayOfWeek = localStart.DayOfWeek;
        var startTime = TimeOnly.FromDateTime(localStart);
        var endTime = TimeOnly.FromDateTime(localEnd);

        // 2. Query repository with all 6 eligibility filters
        var eligibleTechnicians = await _technicianRepository.GetEligibleTechniciansAsync(
            serviceId,
            serviceAreaId,
            dayOfWeek,
            startTime,
            endTime,
            startUtc,
            endUtc,
            bookingId,
            cancellationToken);

        // 3. Case 05 from PDF: No technicians available
        if (eligibleTechnicians.Count == 0)
        {
            return Error.NotFound(
                "Technician.NoneAvailable",
                "Sorry, no available technicians found matching the requested service, area, and time.");
        }

        // 4. Single match
        if (eligibleTechnicians.Count == 1)
        {
            return eligibleTechnicians[0];
        }

        // 5. Random Selection Strategy (Rule from PDF page 10)
        int randomIndex = Random.Shared.Next(eligibleTechnicians.Count);
        return eligibleTechnicians[randomIndex];
    }

    private static TimeZoneInfo GetOperatingTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
