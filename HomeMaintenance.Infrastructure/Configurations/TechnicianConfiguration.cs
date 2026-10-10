namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TechnicianConfiguration : IEntityTypeConfiguration<Technician>
{
    public void Configure(EntityTypeBuilder<Technician> builder)
    {
        builder.ToTable("Technicians");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(t => t.Email)
            .IsUnique();

        builder.Property(t => t.PhoneNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Index on IsActive for performance when querying available technicians
        builder.HasIndex(t => t.IsActive);
    }
}
