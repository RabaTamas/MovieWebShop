using System.Text;
using Microsoft.EntityFrameworkCore;
using MovieShop.OrderService.Data;
using MovieShop.OrderService.DTOs;

namespace MovieShop.OrderService.Services;

public interface IRecommendationService
{
    Task<RecommendationsResultDto> GetRecommendationsAsync(int userId, int count = 5);
    Task<string> GetRecommendationContextAsync(int userId);
}

/// <summary>
/// Az ajánlórendszer az Order Service-be került, mert a bemenete a vásárlási
/// előzmény — az pedig itt él. A filmadatokat a lokális MovieSnapshot projekció
/// szolgáltatja, így mindkét algoritmus egyetlen SQL lekérdezés maradhatott,
/// ugyanúgy, mint a monolitban.
///
/// Ha a filmadatok REST-en érkeznének a Catalog Service-től, a kollaboratív
/// szűrés jelölt-listánként külön hívást igényelne — egy ajánláslekérés több
/// tucat hálózati kört jelentene.
/// </summary>
public class RecommendationService : IRecommendationService
{
    private readonly OrdersDbContext _db;

    public RecommendationService(OrdersDbContext db) => _db = db;

    public async Task<RecommendationsResultDto> GetRecommendationsAsync(int userId, int count = 5)
    {
        var purchasedMovieIds = await GetPurchasedMovieIdsAsync(userId);

        return new RecommendationsResultDto
        {
            CategoryBased = await GetCategoryBasedAsync(purchasedMovieIds, count),
            CollaborativeBased = await GetCollaborativeBasedAsync(userId, purchasedMovieIds, count)
        };
    }

    /// <summary>
    /// Szöveges összefoglaló a Chat Service számára — a nyelvi modell ezt kapja
    /// kontextusként, hogy tudja, mit lát épp a felhasználó az ajánlások oldalon.
    /// </summary>
    public async Task<string> GetRecommendationContextAsync(int userId)
    {
        var purchasedMovieIds = await GetPurchasedMovieIdsAsync(userId);
        if (purchasedMovieIds.Count == 0)
            return string.Empty;

        var result = await GetRecommendationsAsync(userId, 3);
        var sb = new StringBuilder();
        sb.AppendLine("=== PERSONALIZED RECOMMENDATIONS ===");

        if (result.CategoryBased.Count > 0)
        {
            sb.AppendLine("Based on your purchase history, you might enjoy:");
            foreach (var r in result.CategoryBased)
            {
                var price = r.DiscountedPrice ?? r.Price;
                sb.AppendLine($"- {r.Title} ({string.Join(", ", r.Categories)}) - {price} Ft | {r.Reason}");
            }
        }

        if (result.CollaborativeBased.Count > 0)
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

    // ── Kategóriaalapú szűrés ────────────────────────────────────────────────
    // A megvásárolt filmek kategóriáival átfedő, még nem birtokolt filmek,
    // az egyező kategóriák száma és a népszerűség szerint rangsorolva.
    private async Task<List<RecommendationDto>> GetCategoryBasedAsync(List<int> purchasedMovieIds, int count)
    {
        if (purchasedMovieIds.Count == 0)
            return [];

        // A kategóriák a snapshotban pontosvesszős listaként vannak, ezért ezt a
        // lépést memóriában végezzük. A halmaz mérete a felhasználó vásárlásainak
        // száma — néhány tucat elem, nem skálázási kockázat.
        var purchasedSnapshots = await _db.MovieSnapshots
            .Where(m => purchasedMovieIds.Contains(m.MovieId))
            .AsNoTracking()
            .ToListAsync();

        var purchasedCategories = purchasedSnapshots
            .SelectMany(m => m.GetCategories())
            .Distinct()
            .ToList();

        if (purchasedCategories.Count == 0)
            return [];

        // Az eladási darabszám lokális join az OrderMovies táblára
        var orderCounts = await _db.OrderMovies
            .GroupBy(om => om.MovieId)
            .Select(g => new { MovieId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MovieId, x => x.Count);

        var candidates = await _db.MovieSnapshots
            .Where(m => !m.IsDeleted && !purchasedMovieIds.Contains(m.MovieId))
            .AsNoTracking()
            .ToListAsync();

        return candidates
            .Select(m => new
            {
                Snapshot = m,
                MatchCount = m.GetCategories().Count(c => purchasedCategories.Contains(c))
            })
            .Where(x => x.MatchCount > 0)
            .OrderByDescending(x => x.MatchCount)
            .ThenByDescending(x => orderCounts.GetValueOrDefault(x.Snapshot.MovieId, 0))
            .Take(count)
            .Select(x => new RecommendationDto
            {
                Id = x.Snapshot.MovieId,
                Title = x.Snapshot.Title,
                Description = x.Snapshot.Description,
                Price = x.Snapshot.Price,
                DiscountedPrice = x.Snapshot.DiscountedPrice,
                ImageUrl = x.Snapshot.ImageUrl,
                Categories = x.Snapshot.GetCategories(),
                Score = x.MatchCount,
                Reason = $"Because you enjoy {string.Join(", ", purchasedCategories.Take(2))}"
            })
            .ToList();
    }

    // ── Kollaboratív szűrés (felhasználóalapú) ───────────────────────────────
    // Hasonló ízlésű felhasználók keresése, majd az ő vásárlásaikból ajánlás.
    // Ez a lekérdezés gyakorlatilag változatlanul átvehető volt a monolitból,
    // mert az OrderMovies és az Orders ugyanabban az adatbázisban maradt.
    private async Task<List<RecommendationDto>> GetCollaborativeBasedAsync(
        int userId, List<int> purchasedMovieIds, int count)
    {
        if (purchasedMovieIds.Count == 0)
            return [];

        var similarUserIds = await _db.OrderMovies
            .Include(om => om.Order)
            .Where(om => purchasedMovieIds.Contains(om.MovieId) && om.Order.UserId != userId)
            .GroupBy(om => om.Order.UserId)
            .Select(g => new { UserId = g.Key, CommonCount = g.Count() })
            .OrderByDescending(x => x.CommonCount)
            .Take(20)
            .Select(x => x.UserId)
            .ToListAsync();

        if (similarUserIds.Count == 0)
            return [];

        var candidates = await _db.OrderMovies
            .Include(om => om.Order)
            .Where(om => similarUserIds.Contains(om.Order.UserId) && !purchasedMovieIds.Contains(om.MovieId))
            .GroupBy(om => om.MovieId)
            .Select(g => new
            {
                MovieId = g.Key,
                SimilarUserCount = g.Select(om => om.Order.UserId).Distinct().Count()
            })
            .OrderByDescending(x => x.SimilarUserCount)
            .Take(count * 2) // tartalék a törölt filmek kiszűrésére
            .ToListAsync();

        var movieIds = candidates.Select(c => c.MovieId).ToList();

        var snapshots = await _db.MovieSnapshots
            .Where(m => movieIds.Contains(m.MovieId) && !m.IsDeleted)
            .AsNoTracking()
            .ToDictionaryAsync(m => m.MovieId);

        return candidates
            .Where(c => snapshots.ContainsKey(c.MovieId))
            .Take(count)
            .Select(c =>
            {
                var snapshot = snapshots[c.MovieId];
                return new RecommendationDto
                {
                    Id = snapshot.MovieId,
                    Title = snapshot.Title,
                    Description = snapshot.Description,
                    Price = snapshot.Price,
                    DiscountedPrice = snapshot.DiscountedPrice,
                    ImageUrl = snapshot.ImageUrl,
                    Categories = snapshot.GetCategories(),
                    Score = c.SimilarUserCount,
                    Reason = "Customers with similar taste also bought this"
                };
            })
            .ToList();
    }

    private Task<List<int>> GetPurchasedMovieIdsAsync(int userId)
        => _db.OrderMovies
            .Include(om => om.Order)
            .Where(om => om.Order.UserId == userId)
            .Select(om => om.MovieId)
            .Distinct()
            .ToListAsync();
}
