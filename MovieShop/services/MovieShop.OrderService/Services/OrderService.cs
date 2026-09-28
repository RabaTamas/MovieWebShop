using MassTransit;
using Microsoft.EntityFrameworkCore;
using MovieShop.Contracts.Events;
using MovieShop.OrderService.Data;
using MovieShop.OrderService.DTOs;
using MovieShop.OrderService.Models;

namespace MovieShop.OrderService.Services;

public interface IOrderService
{
    Task<List<OrderDto>> GetOrdersByUserIdAsync(int userId);
    Task<OrderDto?> GetOrderByIdAsync(int orderId, int userId);
    Task<OrderDto?> CreateOrderFromCartAsync(int userId, OrderRequestDto dto);
    Task<List<OrderDto>> GetAllOrdersAsync();
    Task<OrderDto?> GetOrderByIdAdminAsync(int orderId);
    Task<bool> UpdateOrderStatusAsync(int orderId, string status);
    Task<List<OrderDto>> GetOrdersByStatusAsync(string status);
    Task<OrderStatisticsDto> GetOrderStatisticsAsync();
    Task<bool> HasUserPurchasedMovieAsync(int userId, int movieId);
    Task<IEnumerable<int>> GetPurchasedMovieIdsAsync(int userId);
}

public class OrderService : IOrderService
{
    private readonly OrdersDbContext _context;
    private readonly IShoppingCartService _cartService;
    private readonly IUserServiceClient _userService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrdersDbContext context,
        IShoppingCartService cartService,
        IUserServiceClient userService,
        IPublishEndpoint publishEndpoint,
        ILogger<OrderService> logger)
    {
        _context = context;
        _cartService = cartService;
        _userService = userService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public Task<List<OrderDto>> GetOrdersByUserIdAsync(int userId)
        => LoadOrdersAsync(BuildQuery().Where(o => o.UserId == userId));

    public async Task<OrderDto?> GetOrderByIdAsync(int orderId, int userId)
        => (await LoadOrdersAsync(BuildQuery().Where(o => o.Id == orderId && o.UserId == userId))).FirstOrDefault();

    public Task<List<OrderDto>> GetAllOrdersAsync()
        => LoadOrdersAsync(BuildQuery());

    public async Task<OrderDto?> GetOrderByIdAdminAsync(int orderId)
        => (await LoadOrdersAsync(BuildQuery().Where(o => o.Id == orderId))).FirstOrDefault();

    public Task<List<OrderDto>> GetOrdersByStatusAsync(string status)
        => Enum.TryParse<OrderStatus>(status, out _)
            ? LoadOrdersAsync(BuildQuery().Where(o => o.Status == status))
            : Task.FromResult(new List<OrderDto>());

    /// <summary>
    /// Rendelés létrehozása a kosárból, a monolit lépéseivel egyezően:
    ///   1. üres kosárnál nincs rendelés,
    ///   2. a számlázási cím új címként a felhasználóhoz kerül,
    ///   3. rendelés + tételek + kosárürítés egy tranzakcióban,
    ///   4. teljesített fizetésnél Completed állapot.
    ///
    /// A 2. lépés a User Service-ben történik, így nem lehet ugyanannak a tranzakciónak
    /// a része. Ha a rendelés mentése meghiúsul, a már létrehozott címet egy
    /// kompenzáló hívás törli — ez a saga minta legegyszerűbb formája, és ugyanazt a
    /// végeredményt adja, mint a monolit tranzakciójának visszagörgetése.
    /// </summary>
    public async Task<OrderDto?> CreateOrderFromCartAsync(int userId, OrderRequestDto dto)
    {
        var cartDto = await _cartService.GetCartByUserIdAsync(userId);
        if (cartDto.Items.Count == 0)
            return null;

        var billingAddress = await _userService.CreateAddressAsync(dto.BillingAddress);
        if (billingAddress == null)
            return null;

        // A saját tranzakciót végrehajtási stratégiába kell ágyazni, mert az Azure SQL
        // serverless ébredése miatt bekapcsolt EnableRetryOnFailure mellett az EF nem
        // engedi a kézzel indított tranzakciókat. Az újrapróbálkozás így az egész
        // blokkot ismétli meg, nem a tranzakció közepén folytatja.
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalPrice = cartDto.Items.Sum(i => i.PriceAtOrder),

                    BillingAddressId = billingAddress.Id,
                    BillingStreet = billingAddress.Street,
                    BillingCity = billingAddress.City,
                    BillingZip = billingAddress.Zip,

                    ShippingAddressId = null, // digitális termék, nincs szállítás
                    PaymentIntentId = dto.PaymentIntentId,

                    // A fizetést a controller már ellenőrizte a Stripe-nál
                    Status = string.IsNullOrEmpty(dto.PaymentIntentId)
                        ? OrderStatus.Pending.ToString()
                        : OrderStatus.Completed.ToString()
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in cartDto.Items)
                {
                    _context.OrderMovies.Add(new OrderMovie
                    {
                        OrderId = order.Id,
                        MovieId = item.MovieId,
                        Quantity = 1, // digitális licenc, mindig 1
                        PriceAtOrder = item.PriceAtOrder
                    });
                }

                await _context.SaveChangesAsync();
                await _cartService.ClearCartAsync(userId);

                await transaction.CommitAsync();

                // A commit UTÁN publikálunk, hogy meg nem történt rendelésről ne menjen esemény
                if (order.Status == OrderStatus.Completed.ToString())
                    await PublishOrderCompletedAsync(order, cartDto.Items.Select(i => i.MovieId).ToList());

                return await GetOrderByIdAsync(order.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for user {UserId}", userId);
                await transaction.RollbackAsync();

                // Kompenzáció: a User Service-ben már létrehozott cím eltávolítása
                await _userService.DeleteAddressAsync(billingAddress.Id);
                return null;
            }
        });
    }

    /// <summary>
    /// Állapotváltás. A monolitban a streaming-hozzáférés élő lekérdezés volt a
    /// Completed rendelésekre, vagyis a hozzáférés AZONNAL megszűnt, ha egy rendelés
    /// bármilyen más állapotba került — és megmaradt, ha a film egy másik teljesített
    /// rendelésben is szerepelt. Az események ezt pontosan leképezik.
    /// </summary>
    public async Task<bool> UpdateOrderStatusAsync(int orderId, string status)
    {
        try
        {
            if (!Enum.TryParse<OrderStatus>(status, out var newStatus))
                return false;

            var order = await _context.Orders
                .Include(o => o.OrderMovies)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return false;

            var wasCompleted = order.Status == OrderStatus.Completed.ToString();
            var isCompleted = newStatus == OrderStatus.Completed;

            order.Status = status;
            await _context.SaveChangesAsync();

            var movieIds = order.OrderMovies.Select(om => om.MovieId).Distinct().ToList();

            if (!wasCompleted && isCompleted)
            {
                await PublishOrderCompletedAsync(order, movieIds);
            }
            else if (wasCompleted && !isCompleted)
            {
                var stillOwned = (await GetPurchasedMovieIdsAsync(order.UserId)).ToHashSet();
                var revoked = movieIds.Where(id => !stillOwned.Contains(id)).ToList();

                if (revoked.Count > 0)
                {
                    await _publishEndpoint.Publish(new OrderRevoked
                    {
                        OrderId = order.Id,
                        UserId = order.UserId,
                        MovieIds = revoked,
                        Reason = status
                    });
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status of order {OrderId}", orderId);
            return false;
        }
    }

    public async Task<OrderStatisticsDto> GetOrderStatisticsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var stats = new OrderStatisticsDto
        {
            TotalOrders = await _context.Orders.CountAsync(),
            TotalRevenue = await _context.Orders.SumAsync(o => (int?)o.TotalPrice) ?? 0,
            OrdersToday = await _context.Orders.CountAsync(o => o.OrderDate >= today && o.OrderDate < tomorrow),
            RevenueToday = await _context.Orders
                .Where(o => o.OrderDate >= today && o.OrderDate < tomorrow)
                .SumAsync(o => (int?)o.TotalPrice) ?? 0
        };

        var ordersByStatus = await _context.Orders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var group in ordersByStatus)
            stats.OrdersByStatus[group.Status] = group.Count;

        stats.TopSellingMovies = await _context.OrderMovies
            .GroupBy(om => om.MovieId)
            .Select(g => new TopSellingMovieDto
            {
                MovieId = g.Key,
                Title = _context.MovieSnapshots
                    .Where(m => m.MovieId == g.Key)
                    .Select(m => m.Title)
                    .FirstOrDefault() ?? "Unknown movie",
                Quantity = g.Sum(om => om.Quantity),
                Revenue = g.Sum(om => om.Quantity * om.PriceAtOrder)
            })
            .OrderByDescending(m => m.Revenue)
            .Take(5)
            .ToListAsync();

        return stats;
    }

    public Task<bool> HasUserPurchasedMovieAsync(int userId, int movieId)
    {
        var completed = OrderStatus.Completed.ToString();

        return _context.Orders
            .Where(o => o.UserId == userId && o.Status == completed)
            .AnyAsync(o => o.OrderMovies.Any(om => om.MovieId == movieId));
    }

    public async Task<IEnumerable<int>> GetPurchasedMovieIdsAsync(int userId)
    {
        var completed = OrderStatus.Completed.ToString();

        return await _context.Orders
            .Where(o => o.UserId == userId && o.Status == completed)
            .SelectMany(o => o.OrderMovies.Select(om => om.MovieId))
            .Distinct()
            .ToListAsync();
    }

    // ── Segédmetódusok ────────────────────────────────────────────────────────

    private async Task PublishOrderCompletedAsync(Order order, List<int> movieIds)
    {
        await _publishEndpoint.Publish(new OrderCompleted
        {
            OrderId = order.Id,
            UserId = order.UserId,
            MovieIds = movieIds,
            TotalPrice = order.TotalPrice
        });

        _logger.LogInformation("OrderCompleted published for order {OrderId} ({Count} movies)", order.Id, movieIds.Count);
    }

    private IQueryable<Order> BuildQuery()
        => _context.Orders.Include(o => o.OrderMovies).AsNoTracking();

    /// <summary>
    /// Rendelések betöltése és feldúsítása a két projekciós táblából.
    /// Három lekérdezés fut, nem N+1: rendelések, filmcímek, felhasználónevek.
    /// </summary>
    private async Task<List<OrderDto>> LoadOrdersAsync(IQueryable<Order> query)
    {
        var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

        if (orders.Count == 0)
            return [];

        var movieIds = orders.SelectMany(o => o.OrderMovies.Select(om => om.MovieId)).Distinct().ToList();
        var userIds = orders.Select(o => o.UserId).Distinct().ToList();

        var titles = await _context.MovieSnapshots
            .Where(m => movieIds.Contains(m.MovieId))
            .AsNoTracking()
            .ToDictionaryAsync(m => m.MovieId, m => m.Title);

        var users = await _context.UserSnapshots
            .Where(u => userIds.Contains(u.UserId))
            .AsNoTracking()
            .ToDictionaryAsync(u => u.UserId, u => new { u.UserName, u.Email });

        return orders.Select(o => new OrderDto
        {
            Id = o.Id,
            OrderDate = o.OrderDate,
            TotalPrice = o.TotalPrice,
            Status = o.Status,
            UserName = users.TryGetValue(o.UserId, out var u) ? u.UserName : "Unknown user",
            UserEmail = users.TryGetValue(o.UserId, out var u2) ? u2.Email : string.Empty,
            BillingAddress = new AddressDto
            {
                Id = o.BillingAddressId ?? 0,
                Street = o.BillingStreet,
                City = o.BillingCity,
                Zip = o.BillingZip
            },
            Movies = o.OrderMovies.Select(om => new OrderMovieDto
            {
                MovieId = om.MovieId,
                Quantity = om.Quantity,
                PriceAtOrder = om.PriceAtOrder,
                Title = titles.TryGetValue(om.MovieId, out var title) ? title : "Unknown movie"
            }).ToList()
        }).ToList();
    }
}
