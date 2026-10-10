namespace HomeMaintenance.Application.Technicians.Dtos;

public class UnavailablePeriodDto
{
    public Guid Id { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public string? Reason { get; set; }
}

public class AddUnavailablePeriodDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public string? Reason { get; set; }
}
