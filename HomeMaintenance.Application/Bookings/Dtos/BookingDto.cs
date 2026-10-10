namespace HomeMaintenance.Application.Bookings.Dtos;

using HomeMaintenance.Domain.Enums;

public class BookingDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public BookingStatus Status { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<BookingAssignmentDto> Assignments { get; set; } = new();
    public List<BookingStatusHistoryDto> StatusHistories { get; set; } = new();
}
