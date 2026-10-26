namespace HomeMaintenance.Api.Common;

using HomeMaintenance.Domain.Constants;
using HomeMaintenance.Domain.Entities;
using HomeMaintenance.Domain.Entities.Identity;
using HomeMaintenance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Seeds essential system data, including default roles and the initial Administrator, Technician, and Customer accounts.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        // 1. Seed Roles
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                if (roleResult.Succeeded)
                {
                    logger.LogInformation("Role '{Role}' seeded successfully.", role);
                }
            }
        }

        // 2. Seed Default Admin User
        const string adminEmail = "admin@homemaintenance.com";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createAdminResult = await userManager.CreateAsync(adminUser, "Admin@123");
            if (createAdminResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
                logger.LogInformation("Default Admin user '{Email}' seeded successfully.", adminEmail);
            }
        }

        // 3. Seed Default Technician User & Domain Entity
        const string techEmail = "technician@homemaintenance.com";
        var existingTech = await userManager.FindByEmailAsync(techEmail);
        if (existingTech is null)
        {
            var techUser = new ApplicationUser
            {
                UserName = techEmail,
                Email = techEmail,
                FullName = "Tarek Mostafa (Lead Technician)",
                EmailConfirmed = true,
                IsActive = true,
                PhoneNumber = "01011112222",
                CreatedAtUtc = DateTime.UtcNow
            };

            var createTechResult = await userManager.CreateAsync(techUser, "Tech@123");
            if (createTechResult.Succeeded)
            {
                await userManager.AddToRoleAsync(techUser, AppRoles.Technician);
                logger.LogInformation("Default Technician user '{Email}' seeded successfully.", techEmail);
            }
        }

        // Ensure Technician domain entity exists
        var existingTechEntity = await dbContext.Technicians
            .Include(t => t.TechnicianServices)
            .Include(t => t.TechnicianServiceAreas)
            .Include(t => t.WorkingHours)
            .FirstOrDefaultAsync(t => t.Email.ToLower() == techEmail.ToLower());

        if (existingTechEntity is null)
        {
            var newTech = new Technician
            {
                Name = "Tarek Mostafa",
                Email = techEmail,
                PhoneNumber = "01011112222",
                IsActive = true
            };

            // Link to all active services and service areas
            var allServices = await dbContext.Services.Where(s => s.IsActive).ToListAsync();
            foreach (var s in allServices)
            {
                newTech.TechnicianServices.Add(new TechnicianService
                {
                    ServiceId = s.Id,
                    IsActive = true
                });
            }

            var allAreas = await dbContext.ServiceAreas.ToListAsync();
            foreach (var a in allAreas)
            {
                newTech.TechnicianServiceAreas.Add(new TechnicianServiceArea
                {
                    ServiceAreaId = a.Id,
                    IsActive = true
                });
            }

            // Standard working hours 08:00 - 18:00 every day
            for (int day = 0; day <= 6; day++)
            {
                newTech.WorkingHours.Add(new WorkingHour
                {
                    DayOfWeek = (DayOfWeek)day,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(18, 0)
                });
            }

            dbContext.Technicians.Add(newTech);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Technician domain entity for '{Email}' created with services, areas, and schedule.", techEmail);
        }

        // 4. Seed Default Customer User & Domain Entity
        const string customerEmail = "customer@homemaintenance.com";
        var existingCustomer = await userManager.FindByEmailAsync(customerEmail);
        if (existingCustomer is null)
        {
            var customerUser = new ApplicationUser
            {
                UserName = customerEmail,
                Email = customerEmail,
                FullName = "Mariam Adel (Customer)",
                EmailConfirmed = true,
                IsActive = true,
                PhoneNumber = "01033334444",
                CreatedAtUtc = DateTime.UtcNow
            };

            var createCustomerResult = await userManager.CreateAsync(customerUser, "Cust@123");
            if (createCustomerResult.Succeeded)
            {
                await userManager.AddToRoleAsync(customerUser, AppRoles.Customer);
                logger.LogInformation("Default Customer user '{Email}' seeded successfully.", customerEmail);
            }
        }

        // Ensure Customer domain entity exists
        var existingCustomerEntity = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Email.ToLower() == customerEmail.ToLower());

        if (existingCustomerEntity is null)
        {
            var newCust = new Customer
            {
                Name = "Mariam Adel",
                Email = customerEmail,
                PhoneNumber = "01033334444",
                DefaultAddress = "15 Tahrir Square, Downtown, Cairo"
            };

            dbContext.Customers.Add(newCust);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Customer domain entity for '{Email}' created.", customerEmail);
        }
    }
}
