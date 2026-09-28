using MassTransit;
using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.Models;
using MovieShop.Contracts.Events;

namespace MovieShop.CatalogService.Consumers;

/// <summary>
/// Rendelés teljesülésekor jogosultságot ad a megvásárolt filmekre.
///
/// Idempotens: ugyanaz az esemény többszöri kézbesítése esetén sem keletkezik
/// duplikált rekord (a RabbitMQ at-least-once kézbesítést garantál).
/// </summary>
public class OrderCompletedConsumer : IConsumer<OrderCompleted>
{
    private readonly CatalogDbContext _db;
    private readonly ILogger<OrderCompletedConsumer> _logger;

    public OrderCompletedConsumer(CatalogDbContext db, ILogger<OrderCompletedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCompleted> context)
    {
        var msg = context.Message;

        var existing = await _db.Entitlements
            .Where(e => e.UserId == msg.UserId && msg.MovieIds.Contains(e.MovieId))
            .Select(e => e.MovieId)
            .ToListAsync(context.CancellationToken);

        var toGrant = msg.MovieIds.Distinct().Except(existing).ToList();
        if (toGrant.Count == 0)
            return;

        _db.Entitlements.AddRange(toGrant.Select(movieId => new Entitlement
        {
            UserId = msg.UserId,
            MovieId = movieId,
            Source = "Order",
            SourceId = msg.OrderId,
            GrantedAt = msg.OccurredAt
        }));

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Granted {Count} entitlements to user {UserId} from order {OrderId}",
            toGrant.Count, msg.UserId, msg.OrderId);
    }
}

/// <summary>
/// Egy rendelés kikerült a teljesített állapotból. Az esemény csak azokat a filmeket
/// sorolja fel, amelyeket a felhasználó ezután SEMMILYEN teljesített rendelésből nem
/// birtokol (ezt az Order Service számolja ki) — így egy másik rendelésből megvett
/// film hozzáférése megmarad, ahogy a monolit élő lekérdezése is megtartotta.
/// </summary>
public class OrderRevokedConsumer : IConsumer<OrderRevoked>
{
    private readonly CatalogDbContext _db;
    private readonly ILogger<OrderRevokedConsumer> _logger;

    public OrderRevokedConsumer(CatalogDbContext db, ILogger<OrderRevokedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderRevoked> context)
    {
        var msg = context.Message;

        var entitlements = await _db.Entitlements
            .Where(e => e.UserId == msg.UserId
                        && e.Source == "Order"
                        && msg.MovieIds.Contains(e.MovieId))
            .ToListAsync(context.CancellationToken);

        if (entitlements.Count == 0)
            return;

        _db.Entitlements.RemoveRange(entitlements);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Revoked {Count} entitlements of user {UserId} (order {OrderId}, reason: {Reason})",
            entitlements.Count, msg.UserId, msg.OrderId, msg.Reason);
    }
}

/// <summary>
/// Megnyert és kifizetett aukció: ha a tárgy film volt, a nyertes jogosultságot kap.
/// Ez a mikroszervizes változat bővítése — a monolitban a megnyert film nem vált
/// streamelhetővé.
/// </summary>
public class AuctionPaidConsumer : IConsumer<AuctionPaid>
{
    private readonly CatalogDbContext _db;
    private readonly ILogger<AuctionPaidConsumer> _logger;

    public AuctionPaidConsumer(CatalogDbContext db, ILogger<AuctionPaidConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuctionPaid> context)
    {
        var msg = context.Message;

        if (msg.MovieId is not { } movieId)
            return;

        var alreadyGranted = await _db.Entitlements
            .AnyAsync(e => e.UserId == msg.WinnerUserId && e.MovieId == movieId, context.CancellationToken);

        if (alreadyGranted)
            return;

        _db.Entitlements.Add(new Entitlement
        {
            UserId = msg.WinnerUserId,
            MovieId = movieId,
            Source = "Auction",
            SourceId = msg.AuctionId,
            GrantedAt = msg.OccurredAt
        });

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Granted entitlement for movie {MovieId} to winner {UserId} of auction {AuctionId}",
            movieId, msg.WinnerUserId, msg.AuctionId);
    }
}
