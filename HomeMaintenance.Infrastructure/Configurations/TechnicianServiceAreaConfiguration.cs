namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TechnicianServiceAreaConfiguration : IEntityTypeConfiguration<TechnicianServiceArea>
{
    public void Configure(EntityTypeBuilder<TechnicianServiceArea> builder)
    {
        builder.ToTable("TechnicianServiceAreas");

        builder.HasKey(tsa => tsa.Id);

        builder.Property(tsa => tsa.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(tsa => tsa.Technician)
            .WithMany(t => t.TechnicianServiceAreas)
            .HasForeignKey(tsa => tsa.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tsa => tsa.ServiceArea)
            .WithMany(sa => sa.TechnicianServiceAreas)
            .HasForeignKey(tsa => tsa.ServiceAreaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Crucial business rule: A technician cannot be assigned to the same service area twice
        builder.HasIndex(tsa => new { tsa.TechnicianId, tsa.ServiceAreaId })
            .IsUnique();
    }
}
