namespace HomeMaintenance.Infrastructure;

using HomeMaintenance.Domain.Common;
using HomeMaintenance.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using HomeMaintenance.Domain.Repositories;
using HomeMaintenance.Infrastructure.Persistence;
using HomeMaintenance.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Configure ASP.NET Core Identity
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

        // Register Unit of Work and Generic Repository
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IServiceAreaRepository, ServiceAreaRepository>();
        services.AddScoped<IServiceRepository, ServiceRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<ITechnicianRepository, TechnicianRepository>();


        return services;
    }
}
