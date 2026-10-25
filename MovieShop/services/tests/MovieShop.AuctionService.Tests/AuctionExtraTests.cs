using System.Net;
using System.Security.Claims;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.AuctionService.Consumers;
using MovieShop.AuctionService.Controllers;
using MovieShop.AuctionService.Data;
using MovieShop.AuctionService.DTOs;
using MovieShop.AuctionService.Hubs;
using MovieShop.AuctionService.Models;
using MovieShop.Contracts.Events;
using Stripe;

namespace MovieShop.AuctionService.Tests;

internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public static FakeHttpHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(respond(request));
}

/// <summary>Az első N mentést optimista zárolási ütközéssel utasítja el – egyidejű licit szimulációja.</summary>
internal sealed class ConcurrencyConflicts(int failures) : SaveChangesInterceptor
{
    private int _remaining = failures;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<Bid>().Any(e => e.State == EntityState.Added) && _remaining-- > 0)
            throw new DbUpdateConcurrencyException("Egy másik licit közben módosította az aukciót");
        return ValueTask.FromResult(result);
    }
}

[Collection("Stripe")]
public class AuctionServiceExtraTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();
    private readonly IStripeClient _originalStripe = StripeConfiguration.StripeClient;

    private AuctionsDbContext Db(SaveChangesInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AuctionsDbContext>().UseInMemoryDatabase(_dbName);
        if (interceptor != null) builder.AddInterceptors(interceptor);
        return new AuctionsDbContext(builder.Options);
    }

    private Services.AuctionService Create(AuctionsDbContext? db = null)
    {
        var hub = Substitute.For<IHubContext<AuctionHub>>();
        hub.Clients.Group(Arg.Any<string>()).Returns(Substitute.For<IClientProxy>());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Stripe:PublishableKey"] = "pk_test" }).Build();
        return new Services.AuctionService(db ?? Db(), hub, config, _publish, NullLogger<Services.AuctionService>.Instance);
    }

    private int Seed(AuctionStatus status, int? bidder = null, bool paid = false, int? movieId = null, string? title = "Tárgy",
        int endsInMinutes = 60, int startsInMinutes = -60)
    {
        using var db = Db();
        var a = new Auction
        {
            AuctionTitle = title, MovieId = movieId, Status = status, CurrentBidderId = bidder, IsPaid = paid,
            StartsAt = DateTime.UtcNow.AddMinutes(startsInMinutes), EndsAt = DateTime.UtcNow.AddMinutes(endsInMinutes),
            StartingPrice = 10_000, CurrentPrice = 25_000
        };
        db.Auctions.Add(a);
        db.SaveChanges();
        return a.Id;
    }

    private static void UseStripe(string json, HttpStatusCode status = HttpStatusCode.OK)
        => StripeConfiguration.StripeClient = new StripeClient("sk_test_dummy",
            httpClient: new SystemNetHttpClient(new HttpClient(FakeHttpHandler.Json(json, status))));

    [Fact]
    public async Task Listings_ActiveWonAndAll()
    {
        Seed(AuctionStatus.Active);
        Seed(AuctionStatus.Pending, startsInMinutes: 30);
        var won = Seed(AuctionStatus.Ended, bidder: 7, endsInMinutes: -10);
        Seed(AuctionStatus.Ended, bidder: 8, endsInMinutes: -10);
        await using (var db = Db())
        {
            db.UserSnapshots.Add(new UserSnapshot { UserId = 7, UserName = "neo" });
            await db.SaveChangesAsync();
        }
        var service = Create();

        Assert.Equal(2, (await service.GetActiveAuctionsAsync()).Count);
        var mine = Assert.Single(await service.GetMyWonAuctionsAsync(7));
        Assert.Equal(won, mine.Id);
        Assert.Equal("neo", mine.CurrentBidderName);
        Assert.Equal(4, (await service.GetAllAuctionsAsync()).Count);
        Assert.Empty(await service.GetMyWonAuctionsAsync(99));
        Assert.Null(await service.GetAuctionAsync(999));
    }

    [Fact]
    public async Task Edit_RecalculatesStatus_AndOnlyPendingPriceIsEditable()
    {
        var pending = Seed(AuctionStatus.Pending, startsInMinutes: 30);
        var active = Seed(AuctionStatus.Active);
        var service = Create();

        var started = await service.EditAuctionAsync(pending, new CreateAuctionRequest
        {
            AuctionTitle = "Új", StartsAt = DateTime.UtcNow.AddMinutes(-1), EndsAt = DateTime.UtcNow.AddHours(1), StartingPrice = 5_000
        });
        Assert.Equal(AuctionStatus.Active, started!.Status);
        Assert.Equal(5_000, started.CurrentPrice);

        var later = await Create().EditAuctionAsync(active, new CreateAuctionRequest
        {
            AuctionTitle = "Később", StartsAt = DateTime.UtcNow.AddHours(1), EndsAt = DateTime.UtcNow.AddHours(2), StartingPrice = 1
        });
        Assert.Equal(AuctionStatus.Pending, later!.Status);
        Assert.Equal(25_000, later.CurrentPrice);

        Assert.Null(await Create().EditAuctionAsync(999, new CreateAuctionRequest()));
        Assert.True(await Create().DeleteAuctionAsync(active));
        Assert.False(await Create().DeleteAuctionAsync(active));
    }

    [Fact]
    public async Task PlaceBid_RetriesAfterConcurrencyConflict()
    {
        var id = Seed(AuctionStatus.Active);

        var result = await Create(Db(new ConcurrencyConflicts(1))).PlaceBidAsync(id, 7, 30_000);

        Assert.True(result.Success);
        await using var db = Db();
        Assert.Single(db.Bids);
    }

    [Fact]
    public async Task PlaceBid_GivesUpAfterThreeConflicts()
    {
        var id = Seed(AuctionStatus.Active);

        var result = await Create(Db(new ConcurrencyConflicts(10))).PlaceBidAsync(id, 7, 30_000);

        Assert.False(result.Success);
        Assert.Contains("high competition", result.Error);
    }

    [Fact]
    public async Task PlaceBid_UnknownAuction()
    {
        Assert.Equal("Auction not found.", (await Create().PlaceBidAsync(999, 7, 1)).Error);
    }

    [Fact]
    public async Task Ending_ResolvesTitleFromMovieSnapshotOrDefault()
    {
        await using (var db = Db())
        {
            db.MovieSnapshots.Add(new MovieSnapshot { MovieId = 3, Title = "Blade Runner" });
            await db.SaveChangesAsync();
        }
        Seed(AuctionStatus.Active, bidder: 7, movieId: 3, title: null, endsInMinutes: -1);
        Seed(AuctionStatus.Active, movieId: 4, title: null, endsInMinutes: -1);
        Seed(AuctionStatus.Active, title: null, endsInMinutes: -1);

        await Create().UpdateAuctionStatusesAsync();

        await _publish.Received(3).Publish(Arg.Any<AuctionEnded>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePayment_OnlyForUnpaidWinner()
    {
        UseStripe("""{"id":"pi_1","object":"payment_intent","client_secret":"pi_1_secret","status":"requires_payment_method"}""");
        var won = Seed(AuctionStatus.Ended, bidder: 7, endsInMinutes: -1);
        var paid = Seed(AuctionStatus.Ended, bidder: 7, paid: true, endsInMinutes: -1);
        var active = Seed(AuctionStatus.Active, bidder: 7);
        var service = Create();

        var result = await service.CreatePaymentIntentAsync(won, 7);
        Assert.Equal(("pi_1_secret", "pk_test"), result!.Value);
        Assert.Null(await service.CreatePaymentIntentAsync(won, 8));
        Assert.Null(await service.CreatePaymentIntentAsync(paid, 7));
        Assert.Null(await service.CreatePaymentIntentAsync(active, 7));
        Assert.Null(await service.CreatePaymentIntentAsync(999, 7));
    }

    [Fact]
    public async Task ConfirmPayment_MarksPaid_AndPublishesAuctionPaid()
    {
        var won = Seed(AuctionStatus.Ended, bidder: 7, movieId: 3, endsInMinutes: -1);
        var unpaidStripe = Seed(AuctionStatus.Ended, bidder: 7, endsInMinutes: -1);
        var paid = Seed(AuctionStatus.Ended, bidder: 7, paid: true, endsInMinutes: -1);

        UseStripe("""{"id":"pi_ok","object":"payment_intent","status":"succeeded"}""");
        Assert.True(await Create().ConfirmPaymentAsync(won, 7, "pi_ok"));
        Assert.True(await Create().ConfirmPaymentAsync(paid, 7, "pi_ok"));
        Assert.False(await Create().ConfirmPaymentAsync(won, 8, "pi_ok"));
        Assert.False(await Create().ConfirmPaymentAsync(999, 7, "pi_ok"));

        UseStripe("""{"id":"pi_no","object":"payment_intent","status":"requires_payment_method"}""");
        Assert.False(await Create().ConfirmPaymentAsync(unpaidStripe, 7, "pi_no"));

        await _publish.Received(1).Publish(Arg.Is<AuctionPaid>(e => e.AuctionId == won && e.MovieId == 3 && e.Amount == 25_000), Arg.Any<CancellationToken>());
        await using var db = Db();
        Assert.True((await db.Auctions.FindAsync(won))!.IsPaid);
    }

    public void Dispose() => StripeConfiguration.StripeClient = _originalStripe;
}

public class AuctionControllerTests
{
    private readonly Services.IAuctionService _service = Substitute.For<Services.IAuctionService>();

    private AuctionController Create(int? userId = 7)
    {
        var c = new AuctionController(_service);
        var identity = userId == null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString()!)], "Test");
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return c;
    }

    [Fact]
    public async Task ReadAndAdminEndpoints()
    {
        _service.GetAuctionAsync(1).Returns(new AuctionDto { Id = 1 });
        _service.CreateAuctionAsync(Arg.Any<CreateAuctionRequest>()).Returns(new AuctionDto { Id = 5 });
        _service.EditAuctionAsync(1, Arg.Any<CreateAuctionRequest>()).Returns(new AuctionDto { Id = 1 });
        _service.DeleteAuctionAsync(1).Returns(true);
        var c = Create();

        Assert.IsType<OkObjectResult>(await c.GetAuctions());
        Assert.IsType<OkObjectResult>(await c.GetAuction(1));
        Assert.IsType<NotFoundResult>(await c.GetAuction(2));
        Assert.IsType<OkObjectResult>(await c.GetMyWonAuctions());
        Assert.IsType<OkObjectResult>(await c.GetAllAuctions());
        Assert.IsType<CreatedAtActionResult>(await c.CreateAuction(new CreateAuctionRequest()));
        Assert.IsType<OkObjectResult>(await c.EditAuction(1, new CreateAuctionRequest()));
        Assert.IsType<NotFoundResult>(await c.EditAuction(2, new CreateAuctionRequest()));
        Assert.IsType<NoContentResult>(await c.DeleteAuction(1));
        Assert.IsType<NotFoundResult>(await c.DeleteAuction(2));
    }

    [Fact]
    public async Task BidAndPayment_RequireUser_AndMapErrors()
    {
        _service.PlaceBidAsync(1, 7, 100).Returns(new BidResult { Success = true });
        _service.PlaceBidAsync(1, 7, 1).Returns(new BidResult { Success = false, Error = "Minimum raise" });
        _service.CreatePaymentIntentAsync(1, 7).Returns(("secret", "pk"));
        _service.ConfirmPaymentAsync(1, 7, "pi_ok").Returns(true);
        var c = Create();

        Assert.IsType<OkObjectResult>(await c.PlaceBid(1, new PlaceBidRequest { Amount = 100 }));
        Assert.IsType<BadRequestObjectResult>(await c.PlaceBid(1, new PlaceBidRequest { Amount = 1 }));
        Assert.IsType<OkObjectResult>(await c.CreatePayment(1));
        Assert.IsType<BadRequestObjectResult>(await c.CreatePayment(2));
        Assert.IsType<OkObjectResult>(await c.ConfirmPayment(1, new ConfirmAuctionPaymentRequest { PaymentIntentId = "pi_ok" }));
        Assert.IsType<BadRequestObjectResult>(await c.ConfirmPayment(1, new ConfirmAuctionPaymentRequest { PaymentIntentId = "pi_bad" }));

        var anonymous = Create(userId: null);
        Assert.IsType<UnauthorizedResult>(await anonymous.GetMyWonAuctions());
        Assert.IsType<UnauthorizedResult>(await anonymous.CreatePayment(1));
        Assert.IsType<UnauthorizedResult>(await anonymous.ConfirmPayment(1, new ConfirmAuctionPaymentRequest()));
        Assert.IsType<UnauthorizedResult>(await anonymous.PlaceBid(1, new PlaceBidRequest()));
    }
}

public class AuctionConsumerAndHubTests
{
    private readonly DbContextOptions<AuctionsDbContext> _options =
        new DbContextOptionsBuilder<AuctionsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static ConsumeContext<T> Context<T>(T msg) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(msg);
        return context;
    }

    [Fact]
    public async Task MovieSnapshots_UpsertAndDropStale()
    {
        var now = DateTime.UtcNow;
        await new MovieChangedConsumer(new AuctionsDbContext(_options)).Consume(Context(new MovieChanged { MovieId = 1, Title = "Új", Price = 1, OccurredAt = now }));
        await new MovieChangedConsumer(new AuctionsDbContext(_options)).Consume(Context(new MovieChanged { MovieId = 1, Title = "Régi", Price = 1, OccurredAt = now.AddMinutes(-1) }));
        await new MovieChangedConsumer(new AuctionsDbContext(_options)).Consume(Context(new MovieChanged { MovieId = 1, Title = "Legújabb", Price = 1, IsDeleted = true, OccurredAt = now.AddMinutes(1) }));

        await using var db = new AuctionsDbContext(_options);
        var snapshot = await db.MovieSnapshots.SingleAsync();
        Assert.Equal("Legújabb", snapshot.Title);
        Assert.True(snapshot.IsDeleted);
    }

    [Fact]
    public async Task UserSnapshots_UpsertStaleAndDeletion()
    {
        var now = DateTime.UtcNow;
        await using (var db = new AuctionsDbContext(_options))
        {
            db.Auctions.Add(new Auction { CurrentBidderId = 7, AuctionTitle = "x", StartsAt = now, EndsAt = now });
            await db.SaveChangesAsync();
        }

        await new UserChangedConsumer(new AuctionsDbContext(_options)).Consume(Context(new UserChanged { UserId = 7, UserName = "neo", OccurredAt = now }));
        await new UserChangedConsumer(new AuctionsDbContext(_options)).Consume(Context(new UserChanged { UserId = 7, UserName = "régi", OccurredAt = now.AddMinutes(-1) }));
        await new UserChangedConsumer(new AuctionsDbContext(_options)).Consume(Context(new UserChanged { UserId = 7, UserName = "neo2", OccurredAt = now.AddMinutes(1) }));
        await using (var db = new AuctionsDbContext(_options))
            Assert.Equal("neo2", (await db.UserSnapshots.SingleAsync()).UserName);

        await new UserChangedConsumer(new AuctionsDbContext(_options)).Consume(Context(new UserChanged { UserId = 7, UserName = "neo2", IsDeleted = true }));
        await new UserChangedConsumer(new AuctionsDbContext(_options)).Consume(Context(new UserChanged { UserId = 8, UserName = "x", IsDeleted = true }));

        await using var check = new AuctionsDbContext(_options);
        Assert.Empty(check.UserSnapshots);
        Assert.Null((await check.Auctions.SingleAsync()).CurrentBidderId);
    }

    [Fact]
    public async Task InternalHasBids()
    {
        await using (var db = new AuctionsDbContext(_options))
        {
            var a = new Auction { AuctionTitle = "x" };
            db.Auctions.Add(a);
            await db.SaveChangesAsync();
            db.Bids.Add(new Bid { AuctionId = a.Id, UserId = 7, Amount = 1 });
            await db.SaveChangesAsync();
        }
        var c = new InternalController(new AuctionsDbContext(_options));

        object Has(IActionResult r) { var v = ((OkObjectResult)r).Value!; return v.GetType().GetProperty("hasBids")!.GetValue(v)!; }
        Assert.Equal(true, Has(await c.HasBids(7)));
        Assert.Equal(false, Has(await c.HasBids(8)));
    }

    [Fact]
    public async Task Hub_JoinsAndLeavesAuctionGroups()
    {
        var groups = Substitute.For<IGroupManager>();
        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns("c1");
        var hub = new AuctionHub { Groups = groups, Context = context };

        await hub.JoinAuction(7);
        await hub.LeaveAuction(7);

        await groups.Received(1).AddToGroupAsync("c1", "auction_7", Arg.Any<CancellationToken>());
        await groups.Received(1).RemoveFromGroupAsync("c1", "auction_7", Arg.Any<CancellationToken>());
    }
}
