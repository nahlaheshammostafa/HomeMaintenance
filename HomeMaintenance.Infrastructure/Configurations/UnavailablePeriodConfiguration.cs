namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UnavailablePeriodConfiguration : IEntityTypeConfiguration<UnavailablePeriod>
{
    public void Configure(EntityTypeBuilder<UnavailablePeriod> builder)
    {
        builder.ToTable("UnavailablePeriods");

        builder.HasKey(up => up.Id);

        builder.Property(up => up.StartTimeUtc)
            .IsRequired();

        builder.Property(up => up.EndTimeUtc)
            .IsRequired();

        builder.Property(up => up.Reason)
            .HasMaxLength(500);

        builder.HasOne(up => up.Technician)
            .WithMany(t => t.UnavailablePeriods)
            .HasForeignKey(up => up.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for fast overlap checking
        builder.HasIndex(up => new { up.TechnicianId, up.StartTimeUtc, up.EndTimeUtc });
    }
}
