namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TechnicianServiceConfiguration : IEntityTypeConfiguration<TechnicianService>
{
    public void Configure(EntityTypeBuilder<TechnicianService> builder)
    {
        builder.ToTable("TechnicianServices");

        builder.HasKey(ts => ts.Id);

        builder.Property(ts => ts.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(ts => ts.Technician)
            .WithMany(t => t.TechnicianServices)
            .HasForeignKey(ts => ts.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ts => ts.Service)
            .WithMany(s => s.TechnicianServices)
            .HasForeignKey(ts => ts.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Crucial business rule: A technician cannot be assigned to the same service twice
        builder.HasIndex(ts => new { ts.TechnicianId, ts.ServiceId })
            .IsUnique();
    }
}
