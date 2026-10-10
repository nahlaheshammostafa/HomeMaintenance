namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {
        builder.ToTable("BookingStatusHistories");

        builder.HasKey(bsh => bsh.Id);

        builder.Property(bsh => bsh.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(bsh => bsh.ChangedAtUtc)
            .IsRequired();

        builder.Property(bsh => bsh.Reason)
            .HasMaxLength(500);

        builder.HasOne(bsh => bsh.Booking)
            .WithMany(b => b.BookingStatusHistories)
            .HasForeignKey(bsh => bsh.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for chronological timeline retrieval
        builder.HasIndex(bsh => new { bsh.BookingId, bsh.ChangedAtUtc });
    }
}
