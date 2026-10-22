using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.AuctionService.Data;
using MovieShop.AuctionService.DTOs;
using MovieShop.AuctionService.Hubs;
using MovieShop.AuctionService.Models;
using MovieShop.Contracts.Events;

namespace MovieShop.AuctionService.Tests;

public class AuctionServiceTests
{
    private const int Alice = 1;
    private const int Bob = 2;

    private readonly DbContextOptions<AuctionsDbContext> _options =
        new DbContextOptionsBuilder<AuctionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private readonly IHubContext<AuctionHub> _hub = Substitute.For<IHubContext<AuctionHub>>();
    private readonly IClientProxy _group = Substitute.For<IClientProxy>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    public AuctionServiceTests()
    {
        _hub.Clients.Group(Arg.Any<string>()).Returns(_group);
    }

    private Services.AuctionService CreateService()
        => new(new AuctionsDbContext(_options), _hub, new ConfigurationBuilder().Build(),
            _publish, NullLogger<Services.AuctionService>.Instance);

    private async Task<int> SeedAuctionAsync(
        decimal startingPrice = 10_000m,
        AuctionStatus status = AuctionStatus.Active,
        TimeSpan? endsIn = null,
        int? currentBidder = null,
        int? movieId = null)
    {
        await using var db = new AuctionsDbContext(_options);
        var auction = new Auction
        {
            AuctionTitle = "Signed poster",
            MovieId = movieId,
            StartsAt = DateTime.UtcNow.AddHours(-1),
            EndsAt = DateTime.UtcNow.Add(endsIn ?? TimeSpan.FromHours(1)),
            StartingPrice = startingPrice,
            CurrentPrice = startingPrice,
            CurrentBidderId = currentBidder,
            Status = status
        };
        db.Auctions.Add(auction);
        await db.SaveChangesAsync();
        return auction.Id;
    }

    private async Task<Auction> LoadAsync(int id)
    {
        await using var db = new AuctionsDbContext(_options);
        return await db.Auctions.Include(a => a.Bids).SingleAsync(a => a.Id == id);
    }

    [Fact]
    public async Task PlaceBid_BelowMinimumIncrement_IsRejected()
    {
        var id = await SeedAuctionAsync(startingPrice: 10_000m);

        // Minimális emelés: az induló ár 1%-a, de legalább 1000 Ft
        var result = await CreateService().PlaceBidAsync(id, Alice, 10_500m);

        Assert.False(result.Success);
        Assert.Contains("Minimum raise is 1", result.Error);
        Assert.Empty((await LoadAsync(id)).Bids);
    }

    [Fact]
    public async Task PlaceBid_ValidBid_UpdatesPrice_RecordsBid_AndNotifiesGroup()
    {
        var id = await SeedAuctionAsync(startingPrice: 10_000m);

        var result = await CreateService().PlaceBidAsync(id, Alice, 11_000m);

        Assert.True(result.Success);
        Assert.Equal(11_000m, result.CurrentPrice);
        Assert.False(result.AntiSnipingTriggered);

        var auction = await LoadAsync(id);
        Assert.Equal(Alice, auction.CurrentBidderId);
        Assert.Single(auction.Bids);

        _hub.Clients.Received(1).Group($"auction_{id}");
        await _group.Received(1).SendCoreAsync("BidPlaced", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlaceBid_HighStartingPrice_UsesOnePercentIncrement()
    {
        var id = await SeedAuctionAsync(startingPrice: 500_000m);

        var tooLow = await CreateService().PlaceBidAsync(id, Alice, 504_000m);
        var enough = await CreateService().PlaceBidAsync(id, Alice, 505_000m);

        Assert.False(tooLow.Success);
        Assert.True(enough.Success);
    }

    [Fact]
    public async Task PlaceBid_CurrentHighestBidder_CannotOutbidThemselves()
    {
        var id = await SeedAuctionAsync(currentBidder: Alice);

        var result = await CreateService().PlaceBidAsync(id, Alice, 20_000m);

        Assert.False(result.Success);
        Assert.Equal("You are already the highest bidder.", result.Error);
    }

    [Fact]
    public async Task PlaceBid_OnPendingAuction_IsRejected()
    {
        var id = await SeedAuctionAsync(status: AuctionStatus.Pending);

        var result = await CreateService().PlaceBidAsync(id, Alice, 20_000m);

        Assert.False(result.Success);
        Assert.Equal("This auction is not active.", result.Error);
    }

    [Fact]
    public async Task PlaceBid_AfterEnd_ClosesAuction()
    {
        var id = await SeedAuctionAsync(endsIn: TimeSpan.FromSeconds(-5));

        var result = await CreateService().PlaceBidAsync(id, Alice, 20_000m);

        Assert.False(result.Success);
        Assert.Equal(AuctionStatus.Ended, (await LoadAsync(id)).Status);
    }

    [Fact]
    public async Task PlaceBid_InLastMinute_ExtendsAuction()
    {
        var id = await SeedAuctionAsync(endsIn: TimeSpan.FromSeconds(30));
        var originalEnd = (await LoadAsync(id)).EndsAt;

        var result = await CreateService().PlaceBidAsync(id, Alice, 11_000m);

        Assert.True(result.Success);
        Assert.True(result.AntiSnipingTriggered);
        Assert.Equal(originalEnd.AddSeconds(60), result.NewEndsAt);
    }

    [Fact]
    public async Task PlaceBid_Outbid_ChangesHighestBidder()
    {
        var id = await SeedAuctionAsync();

        await CreateService().PlaceBidAsync(id, Alice, 11_000m);
        var bobResult = await CreateService().PlaceBidAsync(id, Bob, 12_000m);

        Assert.True(bobResult.Success);
        var auction = await LoadAsync(id);
        Assert.Equal(Bob, auction.CurrentBidderId);
        Assert.Equal(2, auction.Bids.Count);
    }

    [Fact]
    public async Task UpdateStatuses_ActivatesStarted_AndEndsExpired_PublishingAuctionEnded()
    {
        var pendingId = await SeedAuctionAsync(status: AuctionStatus.Pending);
        var expiredId = await SeedAuctionAsync(endsIn: TimeSpan.FromSeconds(-1), currentBidder: Bob, movieId: 9);

        await CreateService().UpdateAuctionStatusesAsync();

        Assert.Equal(AuctionStatus.Active, (await LoadAsync(pendingId)).Status);
        Assert.Equal(AuctionStatus.Ended, (await LoadAsync(expiredId)).Status);
        await _publish.Received(1).Publish(
            Arg.Is<AuctionEnded>(e => e.AuctionId == expiredId && e.WinnerUserId == Bob && e.MovieId == 9),
            Arg.Any<CancellationToken>());
        await _group.Received(1).SendCoreAsync("AuctionEnded", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-1, AuctionStatus.Active)]
    [InlineData(2, AuctionStatus.Pending)]
    public async Task CreateAuction_StatusDependsOnStartTime(int startsInHours, AuctionStatus expected)
    {
        var dto = await CreateService().CreateAuctionAsync(new CreateAuctionRequest
        {
            AuctionTitle = "Director's cut",
            StartsAt = DateTime.UtcNow.AddHours(startsInHours),
            EndsAt = DateTime.UtcNow.AddHours(5),
            StartingPrice = 5_000m
        });

        Assert.Equal(expected, dto.Status);
        Assert.Equal(5_000m, dto.CurrentPrice);
        Assert.Equal(1_000m, dto.MinBidIncrement);
    }

    [Fact]
    public async Task GetAuction_TitleFallsBackToMovieSnapshot()
    {
        await using (var db = new AuctionsDbContext(_options))
        {
            db.MovieSnapshots.Add(new MovieSnapshot { MovieId = 9, Title = "Blade Runner", ImageUrl = "br.jpg" });
            db.Auctions.Add(new Auction
            {
                Id = 50, MovieId = 9, AuctionTitle = null,
                StartsAt = DateTime.UtcNow.AddHours(-1), EndsAt = DateTime.UtcNow.AddHours(1),
                StartingPrice = 1_000m, CurrentPrice = 1_000m, Status = AuctionStatus.Active
            });
            await db.SaveChangesAsync();
        }

        var dto = await CreateService().GetAuctionAsync(50);

        Assert.NotNull(dto);
        Assert.Equal("Blade Runner", dto.Title);
        Assert.Equal("br.jpg", dto.ImageUrl);
    }
}
