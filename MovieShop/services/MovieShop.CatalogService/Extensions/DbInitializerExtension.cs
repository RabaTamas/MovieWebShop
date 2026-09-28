using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.Models;
using MovieShop.CatalogService.Services;

namespace MovieShop.CatalogService.Extensions;

public static class DbInitializerExtension
{
    /// <summary>
    /// A monolit seedelőjének katalógushoz tartozó fele: kategóriák és mintafilmek.
    /// A szerepkörök és az admin felhasználó a User Service-be kerültek.
    /// </summary>
    public static async Task SeedCatalogAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var context = services.GetRequiredService<CatalogDbContext>();
        var movieService = services.GetRequiredService<IMovieService>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        if (!await context.Categories.AnyAsync())
        {
            context.Categories.AddRange(
                new Category { Name = "Action" },
                new Category { Name = "Comedy" },
                new Category { Name = "Drama" },
                new Category { Name = "Horror" },
                new Category { Name = "Sci-Fi" },
                new Category { Name = "Thriller" });

            await context.SaveChangesAsync();
            logger.LogInformation("Alapértelmezett kategóriák létrehozva");
        }

        if (!await context.Movies.AnyAsync())
        {
            context.Movies.AddRange(
                new Movie
                {
                    Title = "The Matrix",
                    Description = "A computer hacker learns from mysterious rebels about the true nature of his reality and his role in the war against its controllers.",
                    Price = 999,
                    DiscountedPrice = 799,
                    ImageUrl = "https://image.tmdb.org/t/p/w500/f89U3ADr1oiB1s9GkdPOEpXUk5H.jpg"
                },
                new Movie
                {
                    Title = "Inception",
                    Description = "A thief who steals corporate secrets through the use of dream-sharing technology is given the inverse task of planting an idea.",
                    Price = 1299,
                    DiscountedPrice = 1099,
                    ImageUrl = "https://image.tmdb.org/t/p/w500/9gk7adHYeDvHkCSEqAvQNLV5Uge.jpg"
                },
                new Movie
                {
                    Title = "The Shawshank Redemption",
                    Description = "Two imprisoned men bond over a number of years, finding solace and eventual redemption through acts of common decency.",
                    Price = 899,
                    ImageUrl = "https://image.tmdb.org/t/p/w500/q6y0Go1tsGEsmtFryDOJo3dEmqu.jpg"
                });

            await context.SaveChangesAsync();

            // A mintafilmeket is publikálni kell, hogy az Order és Auction
            // Service snapshotja azonnal naprakész legyen.
            foreach (var movieId in await context.Movies.Select(m => m.Id).ToListAsync())
                await movieService.PublishMovieChangedAsync(movieId);

            logger.LogInformation("Mintafilmek létrehozva és publikálva");
        }
    }
}
