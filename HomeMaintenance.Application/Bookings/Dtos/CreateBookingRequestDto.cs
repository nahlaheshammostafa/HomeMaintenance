namespace HomeMaintenance.Application.Bookings.Dtos;

public class CreateBookingRequestDto
{
    public Guid CustomerId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid ServiceAreaId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public string Location { get; set; } = string.Empty;
}
