using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.OrderService.Data;
using MovieShop.OrderService.DTOs;
using MovieShop.OrderService.Services;

namespace MovieShop.OrderService.Controllers;

/// <summary>
/// Service-ek közötti végpontok. A gateway nem teszi közzé őket kifelé.
/// </summary>
[ApiController]
[Route("api/internal/orders")]
public class InternalController : ControllerBase
{
    private readonly OrdersDbContext _db;
    private readonly IOrderService _orderService;
    private readonly IRecommendationService _recommendations;

    public InternalController(
        OrdersDbContext db,
        IOrderService orderService,
        IRecommendationService recommendations)
    {
        _db = db;
        _orderService = orderService;
        _recommendations = recommendations;
    }

    /// <summary>
    /// Cím-felhasználási számlálók a User Service admin képernyőihez, kötegelten.
    /// </summary>
    [HttpGet("address-usage")]
    public async Task<ActionResult<List<AddressUsageDto>>> GetAddressUsage([FromQuery] int[] addressIds)
    {
        if (addressIds.Length == 0)
            return Ok(new List<AddressUsageDto>());

        var billing = await _db.Orders
            .Where(o => o.BillingAddressId != null && addressIds.Contains(o.BillingAddressId.Value))
            .GroupBy(o => o.BillingAddressId!.Value)
            .Select(g => new { AddressId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AddressId, x => x.Count);

        var shipping = await _db.Orders
            .Where(o => o.ShippingAddressId != null && addressIds.Contains(o.ShippingAddressId.Value))
            .GroupBy(o => o.ShippingAddressId!.Value)
            .Select(g => new { AddressId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AddressId, x => x.Count);

        return Ok(addressIds
            .Distinct()
            .Select(id => new AddressUsageDto(id, billing.GetValueOrDefault(id, 0), shipping.GetValueOrDefault(id, 0)))
            .ToList());
    }

    /// <summary>
    /// A felhasználó vásárlási kontextusa a Chat Service-nek. Pontosan azokat az
    /// adatokat adja, amiket a monolit ChatService.GetUserContext és a
    /// személyre szabott ajánló ág felhasznált:
    ///   - kosár a filmek AKTUÁLIS árával,
    ///   - a legutóbbi 5 rendelés,
    ///   - minden valaha rendelt film (állapottól függetlenül),
    ///   - az ajánlórendszer szöveges kimenete.
    /// </summary>
    [HttpGet("context/{userId}")]
    public async Task<IActionResult> GetUserContext(int userId)
    {
        var cart = await _db.ShoppingCarts.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);

        var cartRows = cart == null
            ? new List<(int MovieId, int Quantity, int PriceAtOrder)>()
            : (await _db.ShoppingCartMovies
                .AsNoTracking()
                .Where(scm => scm.ShoppingCartId == cart.Id)
                .Select(scm => new { scm.MovieId, scm.Quantity, scm.PriceAtOrder })
                .ToListAsync())
                .Select(r => (r.MovieId, r.Quantity, r.PriceAtOrder))
                .ToList();

        var recentOrders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.OrderMovies)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .ToListAsync();

        var purchasedMovieIds = await _db.OrderMovies
            .AsNoTracking()
            .Where(om => om.Order.UserId == userId)
            .Select(om => om.MovieId)
            .Distinct()
            .ToListAsync();

        var neededIds = cartRows.Select(c => c.MovieId)
            .Concat(recentOrders.SelectMany(o => o.OrderMovies.Select(om => om.MovieId)))
            .Concat(purchasedMovieIds)
            .Distinct()
            .ToList();

        var snapshots = await _db.MovieSnapshots
            .AsNoTracking()
            .Where(m => neededIds.Contains(m.MovieId))
            .ToDictionaryAsync(m => m.MovieId);

        string TitleOf(int movieId) => snapshots.TryGetValue(movieId, out var snap) ? snap.Title : "Unknown movie";

        return Ok(new
        {
            cartItems = cartRows.Select(c => new
            {
                movieId = c.MovieId,
                title = TitleOf(c.MovieId),
                quantity = c.Quantity,
                price = snapshots.TryGetValue(c.MovieId, out var cartSnap)
                    ? cartSnap.DiscountedPrice ?? cartSnap.Price
                    : c.PriceAtOrder
            }),
            recentOrders = recentOrders.Select(o => new
            {
                id = o.Id,
                orderDate = o.OrderDate,
                status = o.Status,
                totalPrice = o.TotalPrice,
                movies = o.OrderMovies.Select(om => TitleOf(om.MovieId)).ToList()
            }),
            purchasedTitles = purchasedMovieIds
                .Where(snapshots.ContainsKey)
                .Select(TitleOf)
                .Distinct()
                .ToList(),
            purchasedMovieIds,
            recommendationContext = await _recommendations.GetRecommendationContextAsync(userId)
        });
    }

    /// <summary>Jogosultság-ellenőrzés: a felhasználó teljesített rendeléseinek filmjei.</summary>
    [HttpGet("purchased/{userId}")]
    public async Task<IActionResult> GetPurchasedMovieIds(int userId)
        => Ok(await _orderService.GetPurchasedMovieIdsAsync(userId));

    /// <summary>
    /// A legtöbbet rendelt, nem törölt filmek a chatbot „népszerű filmek" ágához:
    /// eladott darabszám és aktuális ár, ahogy a monolit is számolta.
    /// </summary>
    [HttpGet("top-movies")]
    public async Task<IActionResult> GetTopMovies([FromQuery] int count = 5)
    {
        var sales = await _db.OrderMovies
            .AsNoTracking()
            .GroupBy(om => om.MovieId)
            .Select(g => new { MovieId = g.Key, OrderCount = g.Sum(om => om.Quantity) })
            .ToListAsync();

        var ids = sales.Select(s => s.MovieId).ToList();

        var snapshots = await _db.MovieSnapshots
            .AsNoTracking()
            .Where(m => ids.Contains(m.MovieId) && !m.IsDeleted)
            .ToDictionaryAsync(m => m.MovieId);

        var top = sales
            .Where(s => snapshots.ContainsKey(s.MovieId))
            .OrderByDescending(s => s.OrderCount)
            .Take(count)
            .Select(s => new
            {
                movieId = s.MovieId,
                title = snapshots[s.MovieId].Title,
                orderCount = s.OrderCount,
                price = snapshots[s.MovieId].DiscountedPrice ?? snapshots[s.MovieId].Price
            });

        return Ok(top);
    }
}
