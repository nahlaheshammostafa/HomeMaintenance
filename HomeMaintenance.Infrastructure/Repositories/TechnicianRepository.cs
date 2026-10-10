namespace HomeMaintenance.Infrastructure.Repositories;

using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Enums;
using HomeMaintenance.Domain.Repositories;
using HomeMaintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class TechnicianRepository : Repository<Technician>, ITechnicianRepository
{
    public TechnicianRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Technician?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(t => t.TechnicianServices)
                .ThenInclude(ts => ts.Service)
            .Include(t => t.TechnicianServiceAreas)
                .ThenInclude(tsa => tsa.ServiceArea)
            .Include(t => t.WorkingHours)
            .Include(t => t.UnavailablePeriods)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Technician>> GetEligibleTechniciansAsync(
        Guid serviceId,
        Guid serviceAreaId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        DateTime startUtc,
        DateTime endUtc,
        Guid? bookingId = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Where(t => t.IsActive)
            // 1. Qualified for the service
            .Where(t => t.TechnicianServices.Any(ts => ts.ServiceId == serviceId && ts.IsActive))
            // 2. Serves the area
            .Where(t => t.TechnicianServiceAreas.Any(tsa => tsa.ServiceAreaId == serviceAreaId && tsa.IsActive))
            // 3. Shift covers the entire duration
            .Where(t => t.WorkingHours.Any(wh => wh.DayOfWeek == dayOfWeek && wh.StartTime <= startTime && wh.EndTime >= endTime))
            // 4. No overlapping unavailable periods
            .Where(t => !t.UnavailablePeriods.Any(up => up.StartTimeUtc < endUtc && up.EndTimeUtc > startUtc))
            // 5. No overlapping confirmed or in-progress bookings
            .Where(t => !t.BookingAssignments.Any(ba =>
                ba.Status == AssignmentStatus.Accepted &&
                (ba.Booking.Status == BookingStatus.Confirmed || ba.Booking.Status == BookingStatus.InProgress) &&
                ba.Booking.StartTimeUtc < endUtc && ba.Booking.EndTimeUtc > startUtc));

        // 6. Exclude technicians who have previously rejected this specific booking
        if (bookingId.HasValue)
        {
            query = query.Where(t => !t.BookingAssignments.Any(ba =>
                ba.BookingId == bookingId.Value &&
                ba.Status == AssignmentStatus.Rejected));
        }

        return await query.ToListAsync(cancellationToken);
    }
}