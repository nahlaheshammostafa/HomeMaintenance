namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.DurationInMinutes)
            .IsRequired();

        builder.Property(s => s.BasePrice)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(s => s.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(s => s.IsActive);

        // Delete behavior: deleting a service referenced by bookings is forbidden (must deactivate instead)
        builder.HasMany(s => s.Bookings)
            .WithOne(b => b.Service)
            .HasForeignKey(b => b.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
