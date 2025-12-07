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

                // Seed sample categories if none exist
                if (!await context.Categories.AnyAsync())
                {
                    var categories = new List<Category>
                    {
                        new Category { Name = "Action" },
                        new Category { Name = "Comedy" },
                        new Category { Name = "Drama" },
                        new Category { Name = "Horror" },
                        new Category { Name = "Sci-Fi" },
                        new Category { Name = "Thriller" }
                    };
                    context.Categories.AddRange(categories);
                    await context.SaveChangesAsync();
                }

                // Seed sample movies if none exist
                if (!await context.Movies.AnyAsync())
                {
                    var actionCategory = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Action");
                    var comedyCategory = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Comedy");
                    var dramaCategory = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Drama");

                    var movies = new List<Movie>
                    {
                        new Movie
                        {
                            Title = "The Matrix",
                            Description = "A computer hacker learns from mysterious rebels about the true nature of his reality and his role in the war against its controllers.",
                            Price = 999,
                            DiscountedPrice = 799,
                            ImageUrl = "https://image.tmdb.org/t/p/w500/f89U3ADr1oiB1s9GkdPOEpXUk5H.jpg",
                            IsDeleted = false,
                            CreatedAt = DateTime.UtcNow
                        },
                        new Movie
                        {
                            Title = "Inception",
                            Description = "A thief who steals corporate secrets through the use of dream-sharing technology is given the inverse task of planting an idea.",
                            Price = 1299,
                            DiscountedPrice = 1099,
                            ImageUrl = "https://image.tmdb.org/t/p/w500/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg",
                            IsDeleted = false,
                            CreatedAt = DateTime.UtcNow
                        },
                        new Movie
                        {
                            Title = "The Shawshank Redemption",
                            Description = "Two imprisoned men bond over a number of years, finding solace and eventual redemption through acts of common decency.",
                            Price = 899,
                            ImageUrl = "https://image.tmdb.org/t/p/w500/q6y0Go1tsGEsmtFryDOJo3dEmqu.jpg",
                            IsDeleted = false,
                            CreatedAt = DateTime.UtcNow
                        }
                    };
                    context.Movies.AddRange(movies);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}