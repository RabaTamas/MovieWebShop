using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;
using System.Text;

namespace MovieShop.Server.Services.Implementations
{
    public class RecommendationService : IRecommendationService
    {
        private readonly AppDbContext _db;

        public RecommendationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<RecommendationsResultDto> GetRecommendationsAsync(int userId, int count = 5)
        {
            var purchasedMovieIds = await GetPurchasedMovieIdsAsync(userId);

            var categoryBased = await GetCategoryBasedAsync(userId, purchasedMovieIds, count);
            var collaborativeBased = await GetCollaborativeBasedAsync(userId, purchasedMovieIds, count);

            return new RecommendationsResultDto
            {
                CategoryBased = categoryBased,
                CollaborativeBased = collaborativeBased
            };
        }

        public async Task<string> GetRecommendationContextAsync(int userId)
        {
            var purchasedMovieIds = await GetPurchasedMovieIdsAsync(userId);
            if (!purchasedMovieIds.Any())
                return string.Empty;

            var result = await GetRecommendationsAsync(userId, 3);
            var sb = new StringBuilder();
            sb.AppendLine("=== PERSONALIZED RECOMMENDATIONS ===");

            if (result.CategoryBased.Any())
            {
                sb.AppendLine("Based on your purchase history, you might enjoy:");
                foreach (var r in result.CategoryBased)
                {
                    var price = r.DiscountedPrice ?? r.Price;
                    sb.AppendLine($"- {r.Title} ({string.Join(", ", r.Categories)}) - {price} Ft | {r.Reason}");
                }
            }

            if (result.CollaborativeBased.Any())
            {
                sb.AppendLine("Customers with similar taste also bought:");
                foreach (var r in result.CollaborativeBased)
                {
                    var price = r.DiscountedPrice ?? r.Price;
                    sb.AppendLine($"- {r.Title} ({string.Join(", ", r.Categories)}) - {price} Ft");
                }
            }

            return sb.ToString();
        }

        // ── Category-overlap algorithm ────────────────────────────────────────
        // Finds movies in categories the user has purchased from, sorted by category match count
        private async Task<List<RecommendationDto>> GetCategoryBasedAsync(
            int userId, List<int> purchasedMovieIds, int count)
        {
            if (!purchasedMovieIds.Any())
                return [];

            // Categories from purchased movies
            var purchasedCategoryIds = await _db.Movies
                .Where(m => purchasedMovieIds.Contains(m.Id))
                .SelectMany(m => m.Categories)
                .Select(c => c.Id)
                .Distinct()
                .ToListAsync();

            if (!purchasedCategoryIds.Any())
                return [];

            var categoryNames = await _db.Categories
                .Where(c => purchasedCategoryIds.Contains(c.Id))
                .Select(c => c.Name)
                .ToListAsync();

            // Unpurchased movies in those categories
            var candidates = await _db.Movies
                .Include(m => m.Categories)
                .Where(m => !m.IsDeleted && !purchasedMovieIds.Contains(m.Id))
                .Where(m => m.Categories.Any(c => purchasedCategoryIds.Contains(c.Id)))
                .Select(m => new
                {
                    m.Id,
                    m.Title,
                    m.Description,
                    m.Price,
                    m.DiscountedPrice,
                    m.ImageUrl,
                    Categories = m.Categories.Select(c => c.Name).ToList(),
                    MatchCount = m.Categories.Count(c => purchasedCategoryIds.Contains(c.Id)),
                    OrderCount = m.OrderMovies.Count()
                })
                .OrderByDescending(x => x.MatchCount)
                .ThenByDescending(x => x.OrderCount)
                .Take(count)
                .ToListAsync();

            return candidates.Select(m => new RecommendationDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description ?? "",
                Price = m.Price,
                DiscountedPrice = m.DiscountedPrice,
                ImageUrl = m.ImageUrl,
                Categories = m.Categories,
                Score = m.MatchCount,
                Reason = $"Because you enjoy {string.Join(", ", categoryNames.Take(2))}"
            }).ToList();
        }

        // ── Collaborative filtering (user-based) ──────────────────────────────
        // Finds users with similar purchases, recommends what they bought
        private async Task<List<RecommendationDto>> GetCollaborativeBasedAsync(
            int userId, List<int> purchasedMovieIds, int count)
        {
            if (!purchasedMovieIds.Any())
                return [];

            // Find similar users: bought at least one same movie
            var similarUserIds = await _db.OrderMovies
                .Include(om => om.Order)
                .Where(om => purchasedMovieIds.Contains(om.MovieId) && om.Order.UserId != userId)
                .GroupBy(om => om.Order.UserId)
                .Select(g => new { UserId = g.Key, CommonCount = g.Count() })
                .OrderByDescending(x => x.CommonCount)
                .Take(20)
                .Select(x => x.UserId)
                .ToListAsync();

            if (!similarUserIds.Any())
                return [];

            // Movies those users bought that current user hasn't
            var candidates = await _db.OrderMovies
                .Include(om => om.Order)
                .Include(om => om.Movie).ThenInclude(m => m.Categories)
                .Where(om =>
                    similarUserIds.Contains(om.Order.UserId) &&
                    !purchasedMovieIds.Contains(om.MovieId) &&
                    !om.Movie.IsDeleted)
                .GroupBy(om => new
                {
                    om.MovieId,
                    om.Movie.Title,
                    om.Movie.Description,
                    om.Movie.Price,
                    om.Movie.DiscountedPrice,
                    om.Movie.ImageUrl
                })
                .Select(g => new
                {
                    g.Key.MovieId,
                    g.Key.Title,
                    g.Key.Description,
                    g.Key.Price,
                    g.Key.DiscountedPrice,
                    g.Key.ImageUrl,
                    SimilarUserCount = g.Select(om => om.Order.UserId).Distinct().Count()
                })
                .OrderByDescending(x => x.SimilarUserCount)
                .Take(count)
                .ToListAsync();

            // Fetch categories separately to avoid EF groupBy issues
            var movieIds = candidates.Select(c => c.MovieId).ToList();
            var movieCategories = await _db.Movies
                .Include(m => m.Categories)
                .Where(m => movieIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Categories.Select(c => c.Name).ToList());

            return candidates.Select(m => new RecommendationDto
            {
                Id = m.MovieId,
                Title = m.Title,
                Description = m.Description ?? "",
                Price = m.Price,
                DiscountedPrice = m.DiscountedPrice,
                ImageUrl = m.ImageUrl,
                Categories = movieCategories.TryGetValue(m.MovieId, out var cats) ? cats : [],
                Score = m.SimilarUserCount,
                Reason = "Customers with similar taste also bought this"
            }).ToList();
        }

        private async Task<List<int>> GetPurchasedMovieIdsAsync(int userId)
        {
            return await _db.OrderMovies
                .Include(om => om.Order)
                .Where(om => om.Order.UserId == userId)
                .Select(om => om.MovieId)
                .Distinct()
                .ToListAsync();
        }
    }
}
