using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Enums;
using HomeMaintenance.Domain.Repositories;
using HomeMaintenance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace HomeMaintenance.Infrastructure.Repositories
{
    public class BookingRepository : Repository<Booking>, IBookingRepository
    {
        public BookingRepository(ApplicationDbContext dbContext) : base(dbContext)
        {
        }
        public async Task<Booking?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(b => b.Customer)
                .Include(b => b.Service)
                .Include(b => b.BookingAssignments)
                    .ThenInclude(ba => ba.Technician)
                        .ThenInclude(t => t.TechnicianServiceAreas)
                .Include(b => b.BookingStatusHistories)
                .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Booking>> GetCustomerBookingsAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(b => b.Customer)
                .Include(b => b.Service)
                .Include(b => b.BookingAssignments)
                    .ThenInclude(ba => ba.Technician)
                .Include(b => b.BookingStatusHistories)
                .Where(b => b.CustomerId == customerId)
                .OrderByDescending(b => b.StartTimeUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Booking>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(b => b.Customer)
                .Include(b => b.Service)
                .Include(b => b.BookingAssignments)
                    .ThenInclude(ba => ba.Technician)
                .Include(b => b.BookingStatusHistories)
                .OrderByDescending(b => b.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasOverlappingBookingAsync(Guid technicianId, DateTime startUtc, DateTime endUtc, Guid? excludeBookingId = null, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(b=>b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress)
                .Where(b=> excludeBookingId == null || b.Id != excludeBookingId)
                .Where(b => b.BookingAssignments.Any(ba => ba.TechnicianId == technicianId && ba.Status == AssignmentStatus.Accepted))
                .AnyAsync(b => (b.StartTimeUtc < endUtc && b.EndTimeUtc > startUtc), cancellationToken);
        }
    }
}
