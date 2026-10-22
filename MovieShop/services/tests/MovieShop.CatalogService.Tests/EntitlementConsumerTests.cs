using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.CatalogService.Consumers;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.Models;
using MovieShop.Contracts.Events;

namespace MovieShop.CatalogService.Tests;

/// <summary>
/// Az Entitlements projekció dönti el, ki melyik filmet streamelheti. A projekciót
/// kizárólag az Order és az Auction Service eseményei építik.
/// </summary>
public class EntitlementConsumerTests
{
    private const int UserId = 5;
    private readonly DbContextOptions<CatalogDbContext> _options =
        new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private CatalogDbContext CreateContext() => new(_options);

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    private Task ConsumeAsync(OrderCompleted message)
        => new OrderCompletedConsumer(CreateContext(), NullLogger<OrderCompletedConsumer>.Instance).Consume(Context(message));

    private Task ConsumeAsync(OrderRevoked message)
        => new OrderRevokedConsumer(CreateContext(), NullLogger<OrderRevokedConsumer>.Instance).Consume(Context(message));

    private Task ConsumeAsync(AuctionPaid message)
        => new AuctionPaidConsumer(CreateContext(), NullLogger<AuctionPaidConsumer>.Instance).Consume(Context(message));

    private async Task<List<Entitlement>> EntitlementsAsync()
    {
        await using var context = CreateContext();
        return await context.Entitlements.OrderBy(e => e.MovieId).ToListAsync();
    }

    [Fact]
    public async Task OrderCompleted_GrantsEntitlementPerMovie()
    {
        await ConsumeAsync(new OrderCompleted { OrderId = 100, UserId = UserId, MovieIds = [1, 2] });

        var entitlements = await EntitlementsAsync();
        Assert.Equal([1, 2], entitlements.Select(e => e.MovieId));
        Assert.All(entitlements, e =>
        {
            Assert.Equal(UserId, e.UserId);
            Assert.Equal("Order", e.Source);
            Assert.Equal(100, e.SourceId);
        });
    }

    [Fact]
    public async Task OrderCompleted_DeliveredTwice_DoesNotDuplicate()
    {
        // A RabbitMQ at-least-once kézbesítést garantál
        var message = new OrderCompleted { OrderId = 100, UserId = UserId, MovieIds = [1, 2] };

        await ConsumeAsync(message);
        await ConsumeAsync(message);

        Assert.Equal(2, (await EntitlementsAsync()).Count);
    }

    [Fact]
    public async Task OrderCompleted_DuplicateMovieIdsInMessage_GrantOnce()
    {
        await ConsumeAsync(new OrderCompleted { OrderId = 100, UserId = UserId, MovieIds = [1, 1] });

        Assert.Single(await EntitlementsAsync());
    }

    [Fact]
    public async Task OrderRevoked_RemovesOnlyListedOrderEntitlements()
    {
        await ConsumeAsync(new OrderCompleted { OrderId = 100, UserId = UserId, MovieIds = [1, 2] });
        await ConsumeAsync(new AuctionPaid { AuctionId = 7, WinnerUserId = UserId, MovieId = 3 });

        await ConsumeAsync(new OrderRevoked { OrderId = 100, UserId = UserId, MovieIds = [2, 3], Reason = "Refunded" });

        // Az 1-es nincs a listában, a 3-as aukcióból származik: mindkettő megmarad
        Assert.Equal([1, 3], (await EntitlementsAsync()).Select(e => e.MovieId));
    }

    [Fact]
    public async Task OrderRevoked_OtherUsersEntitlementsAreUntouched()
    {
        await ConsumeAsync(new OrderCompleted { OrderId = 100, UserId = UserId, MovieIds = [1] });
        await ConsumeAsync(new OrderCompleted { OrderId = 101, UserId = UserId + 1, MovieIds = [1] });

        await ConsumeAsync(new OrderRevoked { OrderId = 100, UserId = UserId, MovieIds = [1], Reason = "Cancelled" });

        var remaining = Assert.Single(await EntitlementsAsync());
        Assert.Equal(UserId + 1, remaining.UserId);
    }

    [Fact]
    public async Task AuctionPaid_ForMovie_GrantsEntitlementOnce()
    {
        var message = new AuctionPaid { AuctionId = 7, WinnerUserId = UserId, MovieId = 3, Amount = 15000 };

        await ConsumeAsync(message);
        await ConsumeAsync(message);

        var entitlement = Assert.Single(await EntitlementsAsync());
        Assert.Equal("Auction", entitlement.Source);
        Assert.Equal(7, entitlement.SourceId);
    }

    [Fact]
    public async Task AuctionPaid_ForCollectible_GrantsNothing()
    {
        await ConsumeAsync(new AuctionPaid { AuctionId = 7, WinnerUserId = UserId, MovieId = null });

        Assert.Empty(await EntitlementsAsync());
    }

    [Fact]
    public async Task AuctionPaid_ForAlreadyPurchasedMovie_KeepsOriginalEntitlement()
    {
        await ConsumeAsync(new OrderCompleted { OrderId = 100, UserId = UserId, MovieIds = [3] });
        await ConsumeAsync(new AuctionPaid { AuctionId = 7, WinnerUserId = UserId, MovieId = 3 });

        var entitlement = Assert.Single(await EntitlementsAsync());
        Assert.Equal("Order", entitlement.Source);
    }
}
