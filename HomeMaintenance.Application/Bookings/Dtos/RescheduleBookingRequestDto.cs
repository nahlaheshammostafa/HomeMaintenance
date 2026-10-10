namespace HomeMaintenance.Application.Bookings.Dtos;

public class RescheduleBookingRequestDto
{
    public Guid ServiceAreaId { get; set; }
    public DateTime NewStartTimeUtc { get; set; }
}
