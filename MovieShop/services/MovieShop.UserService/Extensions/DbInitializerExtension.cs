using MassTransit;
using Microsoft.AspNetCore.Identity;
using MovieShop.Contracts.Events;
using MovieShop.UserService.Constants;
using MovieShop.UserService.Models;

namespace MovieShop.UserService.Extensions;

public static class DbInitializerExtension
{
    /// <summary>
    /// A monolit seedelője itt kettéválik: szerepkörök és admin felhasználó a User
    /// Service-hez tartoznak, a kategóriák és mintafilmek a Catalog Service-hez.
    /// A kosár létrehozása is eltűnt innen — azt az Order Service hozza létre
    /// az első kosárba helyezéskor.
    /// </summary>
    public static async Task SeedRolesAndAdminAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var userManager = services.GetRequiredService<UserManager<User>>();
        var publishEndpoint = services.GetRequiredService<IPublishEndpoint>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        foreach (var role in new[] { UserRoles.Admin, UserRoles.User })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<int>(role));
        }

        // Felhőben (publikus címen) a beégetett jelszó biztonsági kockázat, ezért
        // felülírható konfigurációból. Lokálisan a korábbi érték marad érvényben,
        // hogy a meglévő tesztek és a fejlesztői bejelentkezés változatlanul működjön.
        var configuration = services.GetRequiredService<IConfiguration>();
        var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@movieshop.com";
        var adminPassword = configuration["Seed:AdminPassword"] ?? "Admin123!";

        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new User
            {
                UserName = "Admin",
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, UserRoles.Admin);

                await publishEndpoint.Publish(new UserChanged
                {
                    UserId = admin.Id,
                    UserName = admin.UserName!,
                    Email = admin.Email!,
                    IsDeleted = false
                });

                logger.LogInformation("Admin felhasználó létrehozva: {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Az admin felhasználó létrehozása sikertelen: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
