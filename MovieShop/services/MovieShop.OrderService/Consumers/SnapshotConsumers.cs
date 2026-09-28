using MassTransit;
using Microsoft.EntityFrameworkCore;
using MovieShop.Contracts.Events;
using MovieShop.OrderService.Data;
using MovieShop.OrderService.Models;

namespace MovieShop.OrderService.Consumers;

/// <summary>
/// A filmadatok lokális másolatának karbantartása a Catalog Service eseményeiből.
/// </summary>
public class MovieChangedConsumer : IConsumer<MovieChanged>
{
    private readonly OrdersDbContext _db;
    private readonly ILogger<MovieChangedConsumer> _logger;

    public MovieChangedConsumer(OrdersDbContext db, ILogger<MovieChangedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MovieChanged> context)
    {
        var msg = context.Message;

        var snapshot = await _db.MovieSnapshots
            .FirstOrDefaultAsync(m => m.MovieId == msg.MovieId, context.CancellationToken);

        if (snapshot == null)
        {
            snapshot = new MovieSnapshot { MovieId = msg.MovieId };
            _db.MovieSnapshots.Add(snapshot);
        }
        else if (snapshot.LastUpdatedAt > msg.OccurredAt)
        {
            // Sorrenden kívül érkezett, elavult üzenet
            _logger.LogDebug("Stale MovieChanged dropped for movie {MovieId}", msg.MovieId);
            return;
        }

        snapshot.Title = msg.Title;
        snapshot.Description = msg.Description;
        snapshot.ImageUrl = msg.ImageUrl;
        snapshot.Price = msg.Price;
        snapshot.DiscountedPrice = msg.DiscountedPrice;
        snapshot.CategoriesCsv = string.Join(';', msg.Categories);
        snapshot.IsDeleted = msg.IsDeleted;
        snapshot.LastUpdatedAt = msg.OccurredAt;

        // A kosártételek a monolitban sem törlődtek soft-delete-kor (a Movie rekord
        // megmaradt), ezért itt sem nyúlunk hozzájuk.
        await _db.SaveChangesAsync(context.CancellationToken);
    }
}

/// <summary>
/// Felhasználói adatok másolata az admin rendeléslistákhoz, és a felhasználó
/// törlésekor a hozzá tartozó rendelések és kosár eltávolítása.
/// </summary>
public class UserChangedConsumer : IConsumer<UserChanged>
{
    private readonly OrdersDbContext _db;
    private readonly ILogger<UserChangedConsumer> _logger;

    public UserChangedConsumer(OrdersDbContext db, ILogger<UserChangedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserChanged> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var snapshot = await _db.UserSnapshots.FirstOrDefaultAsync(u => u.UserId == msg.UserId, ct);

        if (msg.IsDeleted)
        {
            // A monolitban az Order → User és a ShoppingCart → User kapcsolat Cascade
            // volt: a felhasználóval együtt törlődtek a rendelései (tételekkel) és a kosara.
            var orders = await _db.Orders.Where(o => o.UserId == msg.UserId).ToListAsync(ct);
            var carts = await _db.ShoppingCarts.Where(c => c.UserId == msg.UserId).ToListAsync(ct);

            _db.Orders.RemoveRange(orders);
            _db.ShoppingCarts.RemoveRange(carts);

            if (snapshot != null)
                _db.UserSnapshots.Remove(snapshot);

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Deleted user {UserId}: removed {Orders} orders and {Carts} carts",
                msg.UserId, orders.Count, carts.Count);
            return;
        }

        if (snapshot == null)
        {
            _db.UserSnapshots.Add(new UserSnapshot
            {
                UserId = msg.UserId,
                UserName = msg.UserName,
                Email = msg.Email,
                LastUpdatedAt = msg.OccurredAt
            });
        }
        else
        {
            if (snapshot.LastUpdatedAt > msg.OccurredAt)
                return;

            snapshot.UserName = msg.UserName;
            snapshot.Email = msg.Email;
            snapshot.LastUpdatedAt = msg.OccurredAt;
        }

        await _db.SaveChangesAsync(ct);
    }
}
