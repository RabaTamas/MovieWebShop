using MassTransit;
using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.Models;
using MovieShop.Contracts.Events;

namespace MovieShop.CatalogService.Consumers;

/// <summary>
/// Felhasználói adatok másolatának karbantartása, és a felhasználó törlésekor a
/// hozzá tartozó katalógusadatok eltávolítása.
/// </summary>
public class UserChangedConsumer : IConsumer<UserChanged>
{
    private readonly CatalogDbContext _db;
    private readonly ILogger<UserChangedConsumer> _logger;

    public UserChangedConsumer(CatalogDbContext db, ILogger<UserChangedConsumer> logger)
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
            // A monolitban a Review → User és a VideoProgress → User kapcsolat Cascade
            // volt, a streaming-hozzáférés pedig a (szintén kaszkádolva törölt)
            // rendelésekből származott. Ugyanezt az eredményt itt eseményből állítjuk elő.
            var reviews = await _db.Reviews.Where(r => r.UserId == msg.UserId).ToListAsync(ct);
            var progresses = await _db.VideoProgresses.Where(vp => vp.UserId == msg.UserId).ToListAsync(ct);
            var entitlements = await _db.Entitlements.Where(e => e.UserId == msg.UserId).ToListAsync(ct);

            _db.Reviews.RemoveRange(reviews);
            _db.VideoProgresses.RemoveRange(progresses);
            _db.Entitlements.RemoveRange(entitlements);

            if (snapshot != null)
                _db.UserSnapshots.Remove(snapshot);

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Deleted user {UserId}: removed {Reviews} reviews, {Progress} progress records, {Entitlements} entitlements",
                msg.UserId, reviews.Count, progresses.Count, entitlements.Count);
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
            // Sorrenden kívül érkezett, elavult üzenet eldobása (last-writer-wins)
            if (snapshot.LastUpdatedAt > msg.OccurredAt)
                return;

            snapshot.UserName = msg.UserName;
            snapshot.Email = msg.Email;
            snapshot.LastUpdatedAt = msg.OccurredAt;
        }

        await _db.SaveChangesAsync(ct);
    }
}
