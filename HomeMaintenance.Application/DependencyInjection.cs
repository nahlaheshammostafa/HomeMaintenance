namespace HomeMaintenance.Application;

using FluentValidation;
using HomeMaintenance.Application.Customers.Contracts;
using HomeMaintenance.Application.Customers.Services;
using HomeMaintenance.Application.ServiceAreas.Contracts;
using HomeMaintenance.Application.ServiceAreas.Services;
using HomeMaintenance.Application.Services.Contracts;
using HomeMaintenance.Application.Services.Services;
using HomeMaintenance.Application.Technicians.Contracts;
using HomeMaintenance.Application.Technicians.Services;
using HomeMaintenance.Application.Bookings.Contracts;
using HomeMaintenance.Application.Bookings.Services;
using Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Automatically registers all FluentValidation validators in this assembly
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Register Application Services
        services.AddScoped<ICustomerAppService, CustomerAppService>();
        services.AddScoped<IServiceAreaAppService, ServiceAreaAppService>();
        services.AddScoped<IServiceAppService, ServiceAppService>();
        services.AddScoped<ITechnicianAppService, TechnicianAppService>();
        services.AddScoped<ITechnicianAssignmentEngine, TechnicianAssignmentEngine>();
        services.AddScoped<IBookingAppService, BookingAppService>();

        return services;
    }
}
