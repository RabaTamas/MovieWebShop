using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.Services;

namespace MovieShop.CatalogService.Controllers;

/// <summary>
/// Service-ek közötti végpontok. A gateway nem teszi közzé őket kifelé.
/// </summary>
[ApiController]
[Route("api/internal/catalog")]
public class InternalController : ControllerBase
{
    private readonly CatalogDbContext _db;
    private readonly IMovieService _movieService;
    private readonly IEntitlementService _entitlements;
    private readonly ILogger<InternalController> _logger;

    public InternalController(
        CatalogDbContext db,
        IMovieService movieService,
        IEntitlementService entitlements,
        ILogger<InternalController> logger)
    {
        _db = db;
        _movieService = movieService;
        _entitlements = entitlements;
        _logger = logger;
    }

    /// <summary>
    /// Minden film újrapublikálása MovieChanged eseményként. Ezzel tölthető fel
    /// kezdeti állapotra az Order és az Auction Service MovieSnapshot táblája.
    /// </summary>
    [HttpPost("republish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RepublishAll()
    {
        var movieIds = await _db.Movies.AsNoTracking().Select(m => m.Id).ToListAsync();

        foreach (var movieId in movieIds)
            await _movieService.PublishMovieChangedAsync(movieId);

        _logger.LogInformation("{Count} movies republished", movieIds.Count);
        return Ok(new { republished = movieIds.Count });
    }

    /// <summary>
    /// A nem törölt filmek kategóriákkal és értékelésekkel. A Chat Service ebből
    /// építi a monolit ChatService kontextusait (film-, kategória- és katalóguskontextus),
    /// és az agent eszközei is ebben keresnek.
    /// </summary>
    [HttpGet("movies")]
    public async Task<IActionResult> GetMovies([FromQuery] int[]? movieIds = null)
    {
        var query = _db.Movies.AsNoTracking().Where(m => !m.IsDeleted);

        if (movieIds is { Length: > 0 })
            query = query.Where(m => movieIds.Contains(m.Id));

        var movies = await query
            .OrderBy(m => m.Id)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.Description,
                m.Price,
                m.DiscountedPrice,
                m.ImageUrl,
                Categories = m.Categories.Select(c => c.Name).ToList(),
                HasVideo = m.VideoFileName != null
            })
            .ToListAsync();

        // Az értékelések és a szerzők neve külön, kötegelt lekérdezéssel
        var ids = movies.Select(m => m.Id).ToList();

        var reviews = await _db.Reviews
            .AsNoTracking()
            .Where(r => ids.Contains(r.MovieId))
            .OrderBy(r => r.Id)
            .Select(r => new { r.MovieId, r.UserId, r.Content })
            .ToListAsync();

        var userIds = reviews.Select(r => r.UserId).Distinct().ToList();

        var userNames = await _db.UserSnapshots
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.UserName);

        var reviewsByMovie = reviews
            .GroupBy(r => r.MovieId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => (object)new
                {
                    userName = userNames.TryGetValue(r.UserId, out var name) ? name : "Anonymous",
                    content = r.Content
                }).ToList());

        return Ok(movies.Select(m => new
        {
            id = m.Id,
            title = m.Title,
            description = m.Description,
            price = m.Price,
            discountedPrice = m.DiscountedPrice,
            imageUrl = m.ImageUrl,
            categories = m.Categories,
            hasVideo = m.HasVideo,
            reviews = reviewsByMovie.TryGetValue(m.Id, out var movieReviews) ? movieReviews : new List<object>()
        }));
    }

    /// <summary>Jogosultság-ellenőrzés más service-ek számára.</summary>
    [HttpGet("entitlements/{userId}")]
    public async Task<IActionResult> GetEntitlements(int userId)
        => Ok(await _entitlements.GetEntitledMovieIdsAsync(userId));
}
