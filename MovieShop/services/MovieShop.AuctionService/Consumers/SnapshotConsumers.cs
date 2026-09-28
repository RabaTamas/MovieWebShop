using MassTransit;
using Microsoft.EntityFrameworkCore;
using MovieShop.AuctionService.Data;
using MovieShop.AuctionService.Models;
using MovieShop.Contracts.Events;

namespace MovieShop.AuctionService.Consumers;

/// <summary>
/// Filmcím és borítókép karbantartása a filmhez kötött aukciókhoz.
/// </summary>
public class MovieChangedConsumer : IConsumer<MovieChanged>
{
    private readonly AuctionsDbContext _db;

    public MovieChangedConsumer(AuctionsDbContext db) => _db = db;

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
            return; // elavult üzenet
        }

        snapshot.Title = msg.Title;
        snapshot.ImageUrl = msg.ImageUrl;
        snapshot.IsDeleted = msg.IsDeleted;
        snapshot.LastUpdatedAt = msg.OccurredAt;

        await _db.SaveChangesAsync(context.CancellationToken);
    }
}

public class UserChangedConsumer : IConsumer<UserChanged>
{
    private readonly AuctionsDbContext _db;

    public UserChangedConsumer(AuctionsDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<UserChanged> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var snapshot = await _db.UserSnapshots.FirstOrDefaultAsync(u => u.UserId == msg.UserId, ct);

        if (msg.IsDeleted)
        {
            // A monolitban az Auction.CurrentBidder kapcsolat SetNull volt
            var auctions = await _db.Auctions.Where(a => a.CurrentBidderId == msg.UserId).ToListAsync(ct);
            foreach (var auction in auctions)
                auction.CurrentBidderId = null;

            if (snapshot != null)
                _db.UserSnapshots.Remove(snapshot);

            await _db.SaveChangesAsync(ct);
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
