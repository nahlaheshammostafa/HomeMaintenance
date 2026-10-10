namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class WorkingHourConfiguration : IEntityTypeConfiguration<WorkingHour>
{
    public void Configure(EntityTypeBuilder<WorkingHour> builder)
    {
        builder.ToTable("WorkingHours");

        builder.HasKey(wh => wh.Id);

        builder.Property(wh => wh.DayOfWeek)
            .IsRequired();

        builder.Property(wh => wh.StartTime)
            .IsRequired();

        builder.Property(wh => wh.EndTime)
            .IsRequired();

        builder.HasOne(wh => wh.Technician)
            .WithMany(t => t.WorkingHours)
            .HasForeignKey(wh => wh.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on (TechnicianId, DayOfWeek) for fast availability filtering
        builder.HasIndex(wh => new { wh.TechnicianId, wh.DayOfWeek });
    }
}
