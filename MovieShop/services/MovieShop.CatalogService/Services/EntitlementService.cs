using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;

namespace MovieShop.CatalogService.Services;

public interface IEntitlementService
{
    Task<bool> HasAccessAsync(int userId, int movieId);
    Task<List<int>> GetEntitledMovieIdsAsync(int userId);
}

/// <summary>
/// A monolit `IOrderService.HasUserPurchasedMovieAsync` metódusának megfelelője.
///
/// A különbség a hívási költségben van: ott ez egy join volt az Orders és
/// OrderMovies táblákra, itt egyetlen indexelt sorolvasás a lokális Entitlements
/// projekcióban. Mivel a streaming végpontok (hls-master, hls-quality) minden
/// egyes playlist-kérésnél meghívják, ez a különbség lejátszás közben számít.
/// </summary>
public class EntitlementService : IEntitlementService
{
    private readonly CatalogDbContext _db;

    public EntitlementService(CatalogDbContext db) => _db = db;

    public Task<bool> HasAccessAsync(int userId, int movieId)
        => _db.Entitlements
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.MovieId == movieId);

    public Task<List<int>> GetEntitledMovieIdsAsync(int userId)
        => _db.Entitlements
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => e.MovieId)
            .Distinct()
            .ToListAsync();
}
