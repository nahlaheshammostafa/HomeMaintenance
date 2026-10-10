namespace HomeMaintenance.Application.Bookings.Dtos;

using HomeMaintenance.Domain.Enums;

public class BookingAssignmentDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public AssignmentStatus Status { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}
