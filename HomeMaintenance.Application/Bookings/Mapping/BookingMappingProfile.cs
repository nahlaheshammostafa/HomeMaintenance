namespace HomeMaintenance.Application.Bookings.Mapping;

using HomeMaintenance.Application.Bookings.Dtos;
using HomeMaintenance.Domain.Entities;

public static class BookingMappingProfile
{
    public static BookingDto ToDto(this Booking entity) =>
        new()
        {
            Id = entity.Id,
            CustomerId = entity.CustomerId,
            CustomerName = entity.Customer?.Name ?? string.Empty,
            ServiceId = entity.ServiceId,
            ServiceName = entity.Service?.Name ?? string.Empty,
            StartTimeUtc = entity.StartTimeUtc,
            EndTimeUtc = entity.EndTimeUtc,
            Status = entity.Status,
            Location = entity.Location,
            CancellationReason = entity.CancellationReason,
            CreatedAtUtc = entity.CreatedAtUtc,
            Assignments = entity.BookingAssignments
                .OrderByDescending(ba => ba.AssignedAtUtc)
                .Select(ba => new BookingAssignmentDto
                {
                    Id = ba.Id,
                    BookingId = ba.BookingId,
                    TechnicianId = ba.TechnicianId,
                    TechnicianName = ba.Technician?.Name ?? string.Empty,
                    Status = ba.Status,
                    AssignedAtUtc = ba.AssignedAtUtc,
                    RespondedAtUtc = ba.RespondedAtUtc
                })
                .ToList(),
            StatusHistories = entity.BookingStatusHistories
                .OrderByDescending(sh => sh.ChangedAtUtc)
                .Select(sh => new BookingStatusHistoryDto
                {
                    Id = sh.Id,
                    BookingId = sh.BookingId,
                    Status = sh.Status,
                    ChangedAtUtc = sh.ChangedAtUtc,
                    Reason = sh.Reason
                })
                .ToList()
        };
}
