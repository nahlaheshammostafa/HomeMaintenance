namespace HomeMaintenance.Application.Technicians.Dtos;

public class WorkingHourDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
