namespace HomeMaintenance.Infrastructure.Configurations;

using HomeMaintenance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.ToTable("ServiceAreas");

        builder.HasKey(sa => sa.Id);

        builder.Property(sa => sa.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasIndex(sa => sa.Name)
            .IsUnique();
    }
}
