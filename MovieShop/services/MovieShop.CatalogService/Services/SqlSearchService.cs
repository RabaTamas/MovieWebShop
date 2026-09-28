using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Models;

namespace MovieShop.CatalogService.Services;

/// <summary>
/// Az Elasticsearch SQL-alapú tartaléka: ugyanazt az interfészt valósítja meg,
/// de közvetlenül az adatbázisból keres, külön keresőmotor nélkül.
///
/// Miért kell: az Azure-ban nincs olcsó menedzselt Elasticsearch, egy saját
/// Elasticsearch konténer pedig memóriaigényes és drága lenne a szakdolgozat
/// költségkeretéhez. Felhőben ezért erre vált a rendszer (Search:Provider=sql),
/// lokálisan marad a teljes értékű Elasticsearch.
///
/// Vállalt korlát a felhős változatban: nincs elgépelés-tűrő (fuzzy) keresés és
/// nincs relevancia-pontozás — a „hary poter” típusú találat csak lokálisan megy.
/// Cserébe nincs indexelési késleltetés: a keresés mindig a friss adatot látja.
/// </summary>
public class SqlSearchService : IElasticsearchService
{
    private readonly CatalogDbContext _context;
    private readonly ILogger<SqlSearchService> _logger;

    public SqlSearchService(CatalogDbContext context, ILogger<SqlSearchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Nincs külön index, ezért az indexelő műveletek nem csinálnak semmit.
    public Task IndexMovieAsync(Movie movie) => Task.CompletedTask;
    public Task DeleteMovieFromIndexAsync(int movieId) => Task.CompletedTask;
    public Task ReindexAllAsync() => Task.CompletedTask;

    public Task EnsureIndexExistsAsync()
    {
        _logger.LogInformation("SQL-alapú keresés aktív — Elasticsearch index nélkül");
        return Task.CompletedTask;
    }

    public async Task<SearchResultDto> SearchAsync(SearchRequestDto req)
    {
        var query = _context.Movies
            .AsNoTracking()
            .Include(m => m.Categories)
            .Include(m => m.Reviews)
            .Where(m => !m.IsDeleted);

        var categories = req.Categories;
        if (categories?.Length > 0)
            query = query.Where(m => m.Categories.Any(c => categories.Contains(c.Name)));

        // Ársáv: ha van akciós ár, az számít — ugyanaz a szabály, mint az Elasticsearch ágban
        if (req.MinPrice.HasValue)
        {
            var min = (int)Math.Floor(req.MinPrice.Value);
            query = query.Where(m => (m.DiscountedPrice ?? m.Price) >= min);
        }

        if (req.MaxPrice.HasValue)
        {
            var max = (int)Math.Ceiling(req.MaxPrice.Value);
            query = query.Where(m => (m.DiscountedPrice ?? m.Price) <= max);
        }

        // Az indexben az átlagos értékelés mindig 0 (a Review modellben nincs pontszám mező),
        // ezért az Elasticsearch ág is üres találatot ad pozitív minimumra — itt ugyanígy.
        if (req.MinRating is > 0)
            query = query.Where(m => false);

        var term = req.Q?.Trim();
        var hasTerm = !string.IsNullOrWhiteSpace(term);

        if (hasTerm)
            query = query.Where(m => m.Title.Contains(term!) || m.Description.Contains(term!));

        query = req.Sort switch
        {
            "price-asc" => query.OrderBy(m => m.DiscountedPrice ?? m.Price),
            "price-desc" => query.OrderByDescending(m => m.DiscountedPrice ?? m.Price),
            "name-asc" => query.OrderBy(m => m.Title),
            "name-desc" => query.OrderByDescending(m => m.Title),
            // Pontszám híján az értékelések száma a legközelebbi értelmes rendezés
            "rating-desc" => query.OrderByDescending(m => m.Reviews.Count).ThenBy(m => m.Title),
            // Relevancia helyett: a címben találók előre, utána cím szerint
            _ => hasTerm
                ? query.OrderByDescending(m => m.Title.Contains(term!) ? 1 : 0).ThenBy(m => m.Title)
                : query.OrderByDescending(m => m.CreatedAt)
        };

        var total = await query.CountAsync();

        var page = req.Page < 1 ? 1 : req.Page;
        var size = req.Size < 1 ? 8 : req.Size;

        var movies = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new SearchResultDto
        {
            Movies = movies.Select(ToDto),
            Total = total,
            Page = page,
            Size = size
        };
    }

    public async Task<IEnumerable<string>> AutocompleteAsync(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return [];

        var start = prefix.Trim();

        return await _context.Movies
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.Title.StartsWith(start))
            .Select(m => m.Title)
            .Distinct()
            .Take(6)
            .ToListAsync();
    }

    private static MovieSearchItemDto ToDto(Movie movie) => new()
    {
        Id = movie.Id,
        Title = movie.Title,
        Description = movie.Description,
        Price = movie.Price,
        DiscountedPrice = movie.DiscountedPrice,
        ImageUrl = movie.ImageUrl,
        Categories = movie.Categories.Select(c => c.Name).ToArray(),
        AverageRating = 0, // a Review modellben nincs pontszám — az Elasticsearch ág is 0-t ad
        ReviewCount = movie.Reviews.Count
    };
}
