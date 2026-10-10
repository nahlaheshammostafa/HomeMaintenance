namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class BookingAssignmentConfiguration : IEntityTypeConfiguration<BookingAssignment>
{
    public void Configure(EntityTypeBuilder<BookingAssignment> builder)
    {
        builder.ToTable("BookingAssignments");

        builder.HasKey(ba => ba.Id);

        builder.Property(ba => ba.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(ba => ba.AssignedAtUtc)
            .IsRequired();

        builder.HasOne(ba => ba.Booking)
            .WithMany(b => b.BookingAssignments)
            .HasForeignKey(ba => ba.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ba => ba.Technician)
            .WithMany(t => t.BookingAssignments)
            .HasForeignKey(ba => ba.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);

        // Core business rule enforced at database level:
        // A technician can only be assigned to a booking ONCE (prevents reassigning rejected technicians)
        builder.HasIndex(ba => new { ba.BookingId, ba.TechnicianId })
            .IsUnique();

        builder.HasIndex(ba => ba.Status);
    }
}
