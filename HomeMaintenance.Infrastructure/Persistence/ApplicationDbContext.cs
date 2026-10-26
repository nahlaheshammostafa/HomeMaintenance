namespace HomeMaintenance.Infrastructure.Persistence;

using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Entities.Identity;
using HomeMaintenance.Core.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core database context for the Home Maintenance platform.
/// Automatically scans and applies entity type configurations from this assembly.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceArea> ServiceAreas => Set<ServiceArea>();
    public DbSet<TechnicianService> TechnicianServices => Set<TechnicianService>();
    public DbSet<TechnicianServiceArea> TechnicianServiceAreas => Set<TechnicianServiceArea>();
    public DbSet<WorkingHour> WorkingHours => Set<WorkingHour>();
    public DbSet<UnavailablePeriod> UnavailablePeriods => Set<UnavailablePeriod>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingAssignment> BookingAssignments => Set<BookingAssignment>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Discovers and applies all IEntityTypeConfiguration<T> in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Ensure all BaseEntity domain entities with Guid primary keys are treated as ValueGenerated.OnAdd
        // so that child entities added to navigation collections with pre-generated GUIDs are recognized
        // by the change tracker as Added (INSERT) instead of Modified (UPDATE).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var primaryKey = entityType.FindPrimaryKey();
                if (primaryKey != null && primaryKey.Properties.Count == 1)
                {
                    var property = primaryKey.Properties[0];
                    if (property.ClrType == typeof(Guid))
                    {
                        property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd;
                    }
                }
            }
        }
    }
}
