using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Hubs;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;
using Stripe;

namespace MovieShop.Server.Services.Implementations
{
    public class AuctionService : IAuctionService
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<AuctionHub> _hub;
        private readonly IConfiguration _config;

        // Anti-sniping: bids placed inside this window extend the auction
        private static readonly TimeSpan AntiSnipingWindow = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan AntiSnipingExtension = TimeSpan.FromSeconds(60);

        public AuctionService(AppDbContext db, IHubContext<AuctionHub> hub, IConfiguration config)
        {
            _db = db;
            _hub = hub;
            _config = config;
            StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        }

        public async Task<List<AuctionDto>> GetActiveAuctionsAsync()
        {
            await UpdateAuctionStatusesAsync();

            return await _db.Auctions
                .Include(a => a.Movie)
                .Include(a => a.CurrentBidder)
                .Where(a => a.Status == AuctionStatus.Active || a.Status == AuctionStatus.Pending)
                .OrderBy(a => a.EndsAt)
                .Select(a => ToDto(a, 0))
                .ToListAsync();
        }

        public async Task<List<AuctionDto>> GetMyWonAuctionsAsync(int userId)
        {
            await UpdateAuctionStatusesAsync();

            return await _db.Auctions
                .Include(a => a.Movie)
                .Include(a => a.CurrentBidder)
                .Where(a => a.Status == AuctionStatus.Ended && a.CurrentBidderId == userId)
                .OrderByDescending(a => a.EndsAt)
                .Select(a => ToDto(a, 0))
                .ToListAsync();
        }

        public async Task<List<AuctionDto>> GetAllAuctionsAsync()
        {
            await UpdateAuctionStatusesAsync();

            return await _db.Auctions
                .Include(a => a.Movie)
                .Include(a => a.CurrentBidder)
                .OrderByDescending(a => a.Id)
                .Select(a => ToDto(a, 0))
                .ToListAsync();
        }

        public async Task<AuctionDto?> GetAuctionAsync(int id)
        {
            await UpdateAuctionStatusesAsync();

            var auction = await _db.Auctions
                .Include(a => a.Movie)
                .Include(a => a.CurrentBidder)
                .Include(a => a.Bids.OrderByDescending(b => b.PlacedAt).Take(20))
                    .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            return auction == null ? null : ToDto(auction, 20);
        }

        public async Task<AuctionDto> CreateAuctionAsync(CreateAuctionRequest request)
        {
            var auction = new Auction
            {
                MovieId       = request.MovieId,
                AuctionTitle  = request.AuctionTitle,
                Description   = request.Description,
                ImageUrl      = request.ImageUrl,
                StartsAt      = request.StartsAt.ToUniversalTime(),
                EndsAt        = request.EndsAt.ToUniversalTime(),
                StartingPrice = request.StartingPrice,
                CurrentPrice  = request.StartingPrice,
                Status        = DateTime.UtcNow >= request.StartsAt.ToUniversalTime()
                                    ? AuctionStatus.Active : AuctionStatus.Pending
            };

            _db.Auctions.Add(auction);
            await _db.SaveChangesAsync();

            return (await GetAuctionAsync(auction.Id))!;
        }

        public async Task<BidResult> PlaceBidAsync(int auctionId, int userId, decimal amount)
        {
            // Retry loop to handle concurrent bids (optimistic locking)
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var auction = await _db.Auctions
                    .Include(a => a.Movie)
                    .FirstOrDefaultAsync(a => a.Id == auctionId);

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
                    return new BidResult { Success = false, Error = $"Minimum raise is {minIncrement:N0} Ft. Your bid must be at least {auction.CurrentPrice + minIncrement:N0} Ft." };

                if (auction.CurrentBidderId == userId)
                    return new BidResult { Success = false, Error = "You are already the highest bidder." };

                // Anti-sniping: extend auction if bid placed in the last 60 seconds
                bool extended = false;
                if (auction.EndsAt - DateTime.UtcNow < AntiSnipingWindow)
                {
                    auction.EndsAt = auction.EndsAt.Add(AntiSnipingExtension);
                    extended = true;
                }

                auction.CurrentPrice    = amount;
                auction.CurrentBidderId = userId;

                var bid = new Bid
                {
                    AuctionId = auctionId,
                    UserId    = userId,
                    Amount    = amount,
                    PlacedAt  = DateTime.UtcNow
                };
                _db.Bids.Add(bid);

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another bid won the race — detach stale entities and retry
                    _db.ChangeTracker.Clear();
                    continue;
                }

                var bidderName = (await _db.Users.FindAsync(userId))?.UserName ?? "Unknown";

                // Broadcast to all watchers in this auction group
                await _hub.Clients.Group($"auction_{auctionId}").SendAsync("BidPlaced", new
                {
                    bidderName,
                    amount,
                    newEndsAt       = auction.EndsAt,
                    antiSniping     = extended,
                    currentPrice    = auction.CurrentPrice
                });

                return new BidResult
                {
                    Success               = true,
                    CurrentPrice          = auction.CurrentPrice,
                    NewEndsAt             = auction.EndsAt,
                    AntiSnipingTriggered  = extended
                };
            }

            return new BidResult { Success = false, Error = "Could not place bid due to high competition. Please try again." };
        }

        public async Task<AuctionDto?> EditAuctionAsync(int id, CreateAuctionRequest request)
        {
            var auction = await _db.Auctions.FindAsync(id);
            if (auction == null) return null;

            // Allow editing any field; changing price on Active auctions resets to current if lower
            auction.MovieId       = request.MovieId;
            auction.AuctionTitle  = request.AuctionTitle;
            auction.Description   = request.Description;
            auction.ImageUrl      = request.ImageUrl;
            auction.StartsAt      = request.StartsAt.ToUniversalTime();
            auction.EndsAt        = request.EndsAt.ToUniversalTime();

            // Only update price if auction hasn't started yet (no bids)
            if (auction.Status == AuctionStatus.Pending)
            {
                auction.StartingPrice = request.StartingPrice;
                auction.CurrentPrice  = request.StartingPrice;
            }

            // Recalculate status from the new dates — an Ended auction with a future EndsAt becomes Active/Pending again
            var now = DateTime.UtcNow;
            if (auction.EndsAt > now)
                auction.Status = auction.StartsAt <= now ? AuctionStatus.Active : AuctionStatus.Pending;

            await _db.SaveChangesAsync();
            return await GetAuctionAsync(id);
        }

        public async Task<bool> DeleteAuctionAsync(int id)
        {
            var auction = await _db.Auctions.FindAsync(id);
            if (auction == null) return false;
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
                Amount   = (long)(auction.CurrentPrice * 100),
                Currency = "huf",
                PaymentMethodTypes = ["card"],
                Metadata = new Dictionary<string, string>
                {
                    ["auctionId"] = auctionId.ToString(),
                    ["userId"]    = userId.ToString()
                }
            };

            var service = new PaymentIntentService();
            var intent  = await service.CreateAsync(options);

            return (intent.ClientSecret, _config["Stripe:PublishableKey"] ?? "");
        }

        public async Task<bool> ConfirmPaymentAsync(int auctionId, int userId, string paymentIntentId)
        {
            var auction = await _db.Auctions.FindAsync(auctionId);
            if (auction == null || auction.Status != AuctionStatus.Ended) return false;
            if (auction.CurrentBidderId != userId) return false;
            if (auction.IsPaid) return true;

            var service = new PaymentIntentService();
            var intent  = await service.GetAsync(paymentIntentId);
            if (intent.Status != "succeeded") return false;

            auction.IsPaid = true;
            await _db.SaveChangesAsync();
            return true;
        }

        // Called by controller and background; transitions Pending→Active and Active→Ended
        public async Task UpdateAuctionStatusesAsync()
        {
            var now = DateTime.UtcNow;

            var toActivate = await _db.Auctions
                .Where(a => a.Status == AuctionStatus.Pending && a.StartsAt <= now)
                .ToListAsync();

            foreach (var a in toActivate)
                a.Status = AuctionStatus.Active;

            var toEnd = await _db.Auctions
                .Include(a => a.Movie)
                .Include(a => a.CurrentBidder)
                .Where(a => a.Status == AuctionStatus.Active && a.EndsAt <= now)
                .ToListAsync();

            foreach (var a in toEnd)
            {
                a.Status = AuctionStatus.Ended;

                // Notify the group that the auction ended
                await _hub.Clients.Group($"auction_{a.Id}").SendAsync("AuctionEnded", new
                {
                    auctionId    = a.Id,
                    winnerName   = a.CurrentBidder?.UserName,
                    finalPrice   = a.CurrentPrice,
                    movieTitle   = a.AuctionTitle ?? a.Movie?.Title ?? "Auction"
                });
            }

            if (toActivate.Count > 0 || toEnd.Count > 0)
                await _db.SaveChangesAsync();
        }

        // 1% of starting price, minimum 1 000 Ft
        private static decimal CalcMinIncrement(decimal startingPrice) =>
            Math.Max(1000m, Math.Round(startingPrice * 0.01m, 0));

        private static AuctionDto ToDto(Auction a, int bidCount) => new()
        {
            Id                 = a.Id,
            MovieId            = a.MovieId,
            Title              = a.AuctionTitle ?? a.Movie?.Title ?? "Auction",
            Description        = a.Description,
            ImageUrl           = a.ImageUrl ?? a.Movie?.ImageUrl ?? string.Empty,
            StartsAt           = DateTime.SpecifyKind(a.StartsAt, DateTimeKind.Utc),
            EndsAt             = DateTime.SpecifyKind(a.EndsAt, DateTimeKind.Utc),
            StartingPrice      = a.StartingPrice,
            CurrentPrice       = a.CurrentPrice,
            MinBidIncrement    = CalcMinIncrement(a.StartingPrice),
            CurrentBidderId    = a.CurrentBidderId,
            CurrentBidderName  = a.CurrentBidder?.UserName,
            Status             = a.Status,
            IsPaid             = a.IsPaid,
            RecentBids         = bidCount > 0
                ? a.Bids.OrderByDescending(b => b.PlacedAt).Take(bidCount)
                      .Select(b => new BidDto
                      {
                          Id         = b.Id,
                          BidderName = b.User?.UserName ?? "?",
                          Amount     = b.Amount,
                          PlacedAt   = b.PlacedAt
                      }).ToList()
                : []
        };
    }
}
