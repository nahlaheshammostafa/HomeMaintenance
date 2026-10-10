namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.StartTimeUtc)
            .IsRequired();

        builder.Property(b => b.EndTimeUtc)
            .IsRequired();

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(b => b.Location)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(b => b.CancellationReason)
            .HasMaxLength(500);

        builder.HasOne(b => b.Customer)
            .WithMany(c => c.Bookings)
            .HasForeignKey(b => b.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Service)
            .WithMany(s => s.Bookings)
            .HasForeignKey(b => b.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Performance indexes for search, scheduler, and conflict detection
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => new { b.StartTimeUtc, b.EndTimeUtc });
        builder.HasIndex(b => b.CustomerId);
    }
}
