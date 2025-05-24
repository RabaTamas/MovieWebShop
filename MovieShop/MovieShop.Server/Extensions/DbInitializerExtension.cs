using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Constants;
using MovieShop.Server.Data;
using MovieShop.Server.Models;

namespace MovieShop.Server.Extensions
{
    public static class DbInitializerExtension
    {
        public static async Task SeedRolesAndAdminAsync(this WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Create roles if they don't exist
                if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(UserRoles.Admin));
                }

                if (!await roleManager.RoleExistsAsync(UserRoles.User))
                {
                    await roleManager.CreateAsync(new IdentityRole<int>(UserRoles.User));
                }

                // Check if admin user exists
                var adminEmail = "admin@movieshop.com";
                var adminUser = await userManager.FindByEmailAsync(adminEmail);

                if (adminUser == null)
                {
                    // Create admin user
                    var admin = new User
                    {
                        UserName = "Admin",
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var result = await userManager.CreateAsync(admin, "Admin123!"); // Change this in production!

                    if (result.Succeeded)
                    {
                        // Assign admin role
                        await userManager.AddToRoleAsync(admin, UserRoles.Admin);

                        // Create shopping cart for admin
                        var cart = new ShoppingCart { UserId = admin.Id };
                        context.ShoppingCarts.Add(cart);
                        await context.SaveChangesAsync();
                    }
                }
            }
        }
    }
}