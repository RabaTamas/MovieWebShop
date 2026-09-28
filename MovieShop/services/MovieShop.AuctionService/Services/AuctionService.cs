using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MovieShop.AuctionService.Data;
using MovieShop.AuctionService.DTOs;
using MovieShop.AuctionService.Hubs;
using MovieShop.AuctionService.Models;
using MovieShop.Contracts.Events;
using Stripe;

namespace MovieShop.AuctionService.Services;

public interface IAuctionService
{
    Task<List<AuctionDto>> GetActiveAuctionsAsync();
    Task<List<AuctionDto>> GetMyWonAuctionsAsync(int userId);
    Task<List<AuctionDto>> GetAllAuctionsAsync();
    Task<AuctionDto?> GetAuctionAsync(int id);
    Task<AuctionDto> CreateAuctionAsync(CreateAuctionRequest request);
    Task<AuctionDto?> EditAuctionAsync(int id, CreateAuctionRequest request);
    Task<bool> DeleteAuctionAsync(int id);
    Task<BidResult> PlaceBidAsync(int auctionId, int userId, decimal amount);
    Task<(string clientSecret, string publishableKey)?> CreatePaymentIntentAsync(int auctionId, int userId);
    Task<bool> ConfirmPaymentAsync(int auctionId, int userId, string paymentIntentId);
    Task UpdateAuctionStatusesAsync();
}

public class AuctionService : IAuctionService
{
    // Anti-sniping: az ezen az ablakon belül leadott licit meghosszabbítja az aukciót
    private static readonly TimeSpan AntiSnipingWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AntiSnipingExtension = TimeSpan.FromSeconds(60);

    private readonly AuctionsDbContext _db;
    private readonly IHubContext<AuctionHub> _hub;
    private readonly IConfiguration _config;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<AuctionService> _logger;

    public AuctionService(
        AuctionsDbContext db,
        IHubContext<AuctionHub> hub,
        IConfiguration config,
        IPublishEndpoint publishEndpoint,
        ILogger<AuctionService> logger)
    {
        _db = db;
        _hub = hub;
        _config = config;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<List<AuctionDto>> GetActiveAuctionsAsync()
    {
        await UpdateAuctionStatusesAsync();

        var auctions = await _db.Auctions
            .Where(a => a.Status == AuctionStatus.Active || a.Status == AuctionStatus.Pending)
            .OrderBy(a => a.EndsAt)
            .AsNoTracking()
            .ToListAsync();

        return await EnrichAsync(auctions, includeBids: false);
    }

    public async Task<List<AuctionDto>> GetMyWonAuctionsAsync(int userId)
    {
        await UpdateAuctionStatusesAsync();

        var auctions = await _db.Auctions
            .Where(a => a.Status == AuctionStatus.Ended && a.CurrentBidderId == userId)
            .OrderByDescending(a => a.EndsAt)
            .AsNoTracking()
            .ToListAsync();

        return await EnrichAsync(auctions, includeBids: false);
    }

    public async Task<List<AuctionDto>> GetAllAuctionsAsync()
    {
        await UpdateAuctionStatusesAsync();

        var auctions = await _db.Auctions
            .OrderByDescending(a => a.Id)
            .AsNoTracking()
            .ToListAsync();

        return await EnrichAsync(auctions, includeBids: false);
    }

    public async Task<AuctionDto?> GetAuctionAsync(int id)
    {
        await UpdateAuctionStatusesAsync();

        var auction = await _db.Auctions
            .Include(a => a.Bids.OrderByDescending(b => b.PlacedAt).Take(20))
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (auction == null)
            return null;

        return (await EnrichAsync([auction], includeBids: true)).First();
    }

    public async Task<AuctionDto> CreateAuctionAsync(CreateAuctionRequest request)
    {
        var startsAt = request.StartsAt.ToUniversalTime();

        var auction = new Auction
        {
            MovieId = request.MovieId,
            AuctionTitle = request.AuctionTitle,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            StartsAt = startsAt,
            EndsAt = request.EndsAt.ToUniversalTime(),
            StartingPrice = request.StartingPrice,
            CurrentPrice = request.StartingPrice,
            Status = DateTime.UtcNow >= startsAt ? AuctionStatus.Active : AuctionStatus.Pending
        };

        _db.Auctions.Add(auction);
        await _db.SaveChangesAsync();

        return (await GetAuctionAsync(auction.Id))!;
    }

    /// <summary>
    /// Licit leadása optimista konkurenciavezérléssel.
    ///
    /// Ez a metódus gyakorlatilag változatlanul átjött a monolitból, és ez
    /// tanulságos: az egész licitelési folyamat — érvényesítés, ármódosítás,
    /// anti-sniping, ütközéskezelés — EGYETLEN adatbázis egyetlen tranzakciójában
    /// zajlik. Épp ezért lehetett az aukciót tisztán kivágni önálló service-be:
    /// a tranzakciós határ nem metszi a service határát.
    /// </summary>
    public async Task<BidResult> PlaceBidAsync(int auctionId, int userId, decimal amount)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var auction = await _db.Auctions.FirstOrDefaultAsync(a => a.Id == auctionId);

            if (auction == null)
                return new BidResult { Success = false, Error = "Auction not found." };

            if (auction.Status != AuctionStatus.Active)
                return new BidResult { Success = false, Error = "This auction is not active." };

            if (DateTime.UtcNow >= auction.EndsAt)
            {
                auction.Status = AuctionStatus.Ended;
                await _db.SaveChangesAsync();
                return new BidResult { Success = false, Error = "This auction has already ended." };
            }

            var minIncrement = CalcMinIncrement(auction.StartingPrice);
            if (amount < auction.CurrentPrice + minIncrement)
            {
                return new BidResult
                {
                    Success = false,
                    Error = $"Minimum raise is {minIncrement:N0} Ft. Your bid must be at least {auction.CurrentPrice + minIncrement:N0} Ft."
                };
            }

            if (auction.CurrentBidderId == userId)
                return new BidResult { Success = false, Error = "You are already the highest bidder." };

            // Anti-sniping: az utolsó 60 másodpercben leadott licit hosszabbít
            var extended = false;
            if (auction.EndsAt - DateTime.UtcNow < AntiSnipingWindow)
            {
                auction.EndsAt = auction.EndsAt.Add(AntiSnipingExtension);
                extended = true;
            }

            auction.CurrentPrice = amount;
            auction.CurrentBidderId = userId;

            _db.Bids.Add(new Bid
            {
                AuctionId = auctionId,
                UserId = userId,
                Amount = amount,
                PlacedAt = DateTime.UtcNow
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Egy másik licit megnyerte a versenyt — friss adattal újrapróbálunk
                _db.ChangeTracker.Clear();
                continue;
            }

            // A licitáló neve a lokális snapshotból — a monolitban
            // `_db.Users.FindAsync(userId)` volt, ami most másik adatbázis lenne.
            var bidderName = await _db.UserSnapshots
                .Where(u => u.UserId == userId)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync() ?? "Unknown";

            await _hub.Clients.Group($"auction_{auctionId}").SendAsync("BidPlaced", new
            {
                bidderName,
                amount,
                newEndsAt = auction.EndsAt,
                antiSniping = extended,
                currentPrice = auction.CurrentPrice
            });

            return new BidResult
            {
                Success = true,
                CurrentPrice = auction.CurrentPrice,
                NewEndsAt = auction.EndsAt,
                AntiSnipingTriggered = extended
            };
        }

        return new BidResult
        {
            Success = false,
            Error = "Could not place bid due to high competition. Please try again."
        };
    }

    public async Task<AuctionDto?> EditAuctionAsync(int id, CreateAuctionRequest request)
    {
        var auction = await _db.Auctions.FindAsync(id);
        if (auction == null)
            return null;

        auction.MovieId = request.MovieId;
        auction.AuctionTitle = request.AuctionTitle;
        auction.Description = request.Description;
        auction.ImageUrl = request.ImageUrl;
        auction.StartsAt = request.StartsAt.ToUniversalTime();
        auction.EndsAt = request.EndsAt.ToUniversalTime();

        // Az árat csak akkor lehet módosítani, ha még nem indult el (nincs licit)
        if (auction.Status == AuctionStatus.Pending)
        {
            auction.StartingPrice = request.StartingPrice;
            auction.CurrentPrice = request.StartingPrice;
        }

        // Állapot újraszámítása a dátumokból
        var now = DateTime.UtcNow;
        if (auction.EndsAt > now)
            auction.Status = auction.StartsAt <= now ? AuctionStatus.Active : AuctionStatus.Pending;

        await _db.SaveChangesAsync();
        return await GetAuctionAsync(id);
    }

    public async Task<bool> DeleteAuctionAsync(int id)
    {
        var auction = await _db.Auctions.FindAsync(id);
        if (auction == null)
            return false;

        _db.Auctions.Remove(auction);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(string clientSecret, string publishableKey)?> CreatePaymentIntentAsync(int auctionId, int userId)
    {
        var auction = await _db.Auctions.FindAsync(auctionId);

        if (auction == null || auction.Status != AuctionStatus.Ended) return null;
        if (auction.CurrentBidderId != userId) return null;
        if (auction.IsPaid) return null;

        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(auction.CurrentPrice * 100),
            Currency = "huf",
            PaymentMethodTypes = ["card"],
            Metadata = new Dictionary<string, string>
            {
                ["auctionId"] = auctionId.ToString(),
                ["userId"] = userId.ToString()
            }
        };

        var intent = await new PaymentIntentService().CreateAsync(options);

        return (intent.ClientSecret, _config["Stripe:PublishableKey"] ?? "");
    }

    /// <summary>
    /// A nyertes kifizette a tételt. Ha filmre szólt az aukció, AuctionPaid
    /// eseményt publikálunk — a Catalog Service ebből ad streamelési jogosultságot.
    ///
    /// Figyeld meg, hogy az Auction Service semmit nem tud a streamingről vagy a
    /// jogosultságokról. Csak annyit közöl, hogy „ezt kifizették"; hogy ebből mi
    /// következik, az a fogadó service dolga.
    /// </summary>
    public async Task<bool> ConfirmPaymentAsync(int auctionId, int userId, string paymentIntentId)
    {
        var auction = await _db.Auctions.FindAsync(auctionId);

        if (auction == null || auction.Status != AuctionStatus.Ended) return false;
        if (auction.CurrentBidderId != userId) return false;
        if (auction.IsPaid) return true;

        var intent = await new PaymentIntentService().GetAsync(paymentIntentId);
        if (intent.Status != "succeeded")
            return false;

        auction.IsPaid = true;
        await _db.SaveChangesAsync();

        await _publishEndpoint.Publish(new AuctionPaid
        {
            AuctionId = auction.Id,
            WinnerUserId = userId,
            MovieId = auction.MovieId,
            Amount = auction.CurrentPrice
        });

        _logger.LogInformation(
            "AuctionPaid publikálva — aukció: {AuctionId}, nyertes: {UserId}", auctionId, userId);

        return true;
    }

    /// <summary>
    /// Állapotátmenetek: Pending→Active és Active→Ended. Minden API-hívás előtt lefut.
    /// </summary>
    public async Task UpdateAuctionStatusesAsync()
    {
        var now = DateTime.UtcNow;

        var toActivate = await _db.Auctions
            .Where(a => a.Status == AuctionStatus.Pending && a.StartsAt <= now)
            .ToListAsync();

        foreach (var auction in toActivate)
            auction.Status = AuctionStatus.Active;

        var toEnd = await _db.Auctions
            .Where(a => a.Status == AuctionStatus.Active && a.EndsAt <= now)
            .ToListAsync();

        foreach (var auction in toEnd)
            auction.Status = AuctionStatus.Ended;

        if (toActivate.Count == 0 && toEnd.Count == 0)
            return;

        await _db.SaveChangesAsync();

        // Az értesítés a mentés UTÁN megy ki, hogy a kliens ne lássa korábban a
        // lezárást, mint ahogy az adatbázisban rögzült.
        foreach (var auction in toEnd)
        {
            var winnerName = auction.CurrentBidderId == null
                ? null
                : await _db.UserSnapshots
                    .Where(u => u.UserId == auction.CurrentBidderId)
                    .Select(u => u.UserName)
                    .FirstOrDefaultAsync();

            var title = await ResolveTitleAsync(auction);

            await _hub.Clients.Group($"auction_{auction.Id}").SendAsync("AuctionEnded", new
            {
                auctionId = auction.Id,
                winnerName,
                finalPrice = auction.CurrentPrice,
                movieTitle = title
            });

            await _publishEndpoint.Publish(new AuctionEnded
            {
                AuctionId = auction.Id,
                WinnerUserId = auction.CurrentBidderId,
                MovieId = auction.MovieId,
                FinalPrice = auction.CurrentPrice
            });
        }
    }

    // ── Segédmetódusok ────────────────────────────────────────────────────────

    /// <summary>Az induló ár 1%-a, de legalább 1000 Ft.</summary>
    private static decimal CalcMinIncrement(decimal startingPrice)
        => Math.Max(1000m, Math.Round(startingPrice * 0.01m, 0));

    private async Task<string> ResolveTitleAsync(Auction auction)
    {
        // A monolit `a.AuctionTitle ?? a.Movie?.Title ?? "Auction"` kifejezésével azonos:
        // üres, de nem null cím esetén a megadott (üres) cím marad.
        if (auction.AuctionTitle != null)
            return auction.AuctionTitle;

        if (auction.MovieId is not { } movieId)
            return "Auction";

        return await _db.MovieSnapshots
            .Where(m => m.MovieId == movieId)
            .Select(m => m.Title)
            .FirstOrDefaultAsync() ?? "Auction";
    }

    /// <summary>
    /// Aukciók feldúsítása a két projekciós táblából. Kötegelt lekérdezés:
    /// az összes szükséges filmcím és felhasználónév egy-egy hívással jön,
    /// nem aukciónként külön (N+1 elkerülése).
    /// </summary>
    private async Task<List<AuctionDto>> EnrichAsync(List<Auction> auctions, bool includeBids)
    {
        if (auctions.Count == 0)
            return [];

        var movieIds = auctions.Where(a => a.MovieId.HasValue).Select(a => a.MovieId!.Value).Distinct().ToList();

        var userIds = auctions
            .Where(a => a.CurrentBidderId.HasValue)
            .Select(a => a.CurrentBidderId!.Value)
            .Concat(auctions.SelectMany(a => a.Bids.Select(b => b.UserId)))
            .Distinct()
            .ToList();

        var movies = movieIds.Count == 0
            ? []
            : await _db.MovieSnapshots
                .Where(m => movieIds.Contains(m.MovieId))
                .AsNoTracking()
                .ToDictionaryAsync(m => m.MovieId);

        var users = userIds.Count == 0
            ? []
            : await _db.UserSnapshots
                .Where(u => userIds.Contains(u.UserId))
                .AsNoTracking()
                .ToDictionaryAsync(u => u.UserId, u => u.UserName);

        return auctions.Select(a =>
        {
            movies.TryGetValue(a.MovieId ?? 0, out var movie);

            return new AuctionDto
            {
                Id = a.Id,
                MovieId = a.MovieId,
                Title = a.AuctionTitle ?? movie?.Title ?? "Auction",
                Description = a.Description,
                ImageUrl = a.ImageUrl ?? movie?.ImageUrl ?? string.Empty,
                StartsAt = DateTime.SpecifyKind(a.StartsAt, DateTimeKind.Utc),
                EndsAt = DateTime.SpecifyKind(a.EndsAt, DateTimeKind.Utc),
                StartingPrice = a.StartingPrice,
                CurrentPrice = a.CurrentPrice,
                MinBidIncrement = CalcMinIncrement(a.StartingPrice),
                CurrentBidderId = a.CurrentBidderId,
                CurrentBidderName = a.CurrentBidderId.HasValue
                    ? users.GetValueOrDefault(a.CurrentBidderId.Value)
                    : null,
                Status = a.Status,
                IsPaid = a.IsPaid,
                RecentBids = includeBids
                    ? a.Bids.OrderByDescending(b => b.PlacedAt).Select(b => new BidDto
                    {
                        Id = b.Id,
                        BidderName = users.GetValueOrDefault(b.UserId, "?"),
                        Amount = b.Amount,
                        PlacedAt = b.PlacedAt
                    }).ToList()
                    : []
            };
        }).ToList();
    }
}
