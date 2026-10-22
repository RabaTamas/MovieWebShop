using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.Contracts.Events;
using MovieShop.OrderService.Consumers;
using MovieShop.OrderService.Models;

namespace MovieShop.OrderService.Tests;

public class SnapshotConsumerTests : IDisposable
{
    private readonly OrdersDb _db = new();

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var context = Substitute.For<ConsumeContext<T>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    private Task ConsumeAsync(MovieChanged message)
        => new MovieChangedConsumer(_db.CreateContext(), NullLogger<MovieChangedConsumer>.Instance)
            .Consume(Context(message));

    private Task ConsumeAsync(UserChanged message)
        => new UserChangedConsumer(_db.CreateContext(), NullLogger<UserChangedConsumer>.Instance)
            .Consume(Context(message));

    [Fact]
    public async Task MovieChanged_CreatesSnapshot()
    {
        await ConsumeAsync(new MovieChanged
        {
            MovieId = 10, Title = "Dune", Price = 1500, DiscountedPrice = 1200,
            Categories = ["Sci-Fi", "Adventure"]
        });

        await using var context = _db.CreateContext();
        var snapshot = await context.MovieSnapshots.SingleAsync();
        Assert.Equal("Dune", snapshot.Title);
        Assert.Equal(1200, snapshot.DiscountedPrice);
        Assert.Equal(["Sci-Fi", "Adventure"], snapshot.GetCategories());
    }

    [Fact]
    public async Task MovieChanged_RedeliveredMessage_IsIdempotent()
    {
        var message = new MovieChanged { MovieId = 10, Title = "Dune", Price = 1500 };

        await ConsumeAsync(message);
        await ConsumeAsync(message);

        await using var context = _db.CreateContext();
        Assert.Equal(1, await context.MovieSnapshots.CountAsync());
    }

    [Fact]
    public async Task MovieChanged_StaleMessage_IsDropped()
    {
        var now = DateTime.UtcNow;
        await ConsumeAsync(new MovieChanged { MovieId = 10, Title = "Új cím", Price = 1500, OccurredAt = now });

        // Egy korábban keletkezett, de később kézbesített üzenet nem írhatja felül
        await ConsumeAsync(new MovieChanged { MovieId = 10, Title = "Régi cím", Price = 900, OccurredAt = now.AddMinutes(-1) });

        await using var context = _db.CreateContext();
        var snapshot = await context.MovieSnapshots.SingleAsync();
        Assert.Equal("Új cím", snapshot.Title);
        Assert.Equal(1500, snapshot.Price);
    }

    [Fact]
    public async Task UserChanged_UpsertsSnapshot()
    {
        await ConsumeAsync(new UserChanged { UserId = 3, UserName = "trinity", Email = "t@zion.io" });
        await ConsumeAsync(new UserChanged { UserId = 3, UserName = "trinity2", Email = "t@zion.io", OccurredAt = DateTime.UtcNow.AddSeconds(1) });

        await using var context = _db.CreateContext();
        Assert.Equal("trinity2", (await context.UserSnapshots.SingleAsync()).UserName);
    }

    [Fact]
    public async Task UserChanged_Deleted_RemovesOrdersCartAndSnapshot()
    {
        _db.SeedOrder(3, OrderStatus.Completed, 1, 2);
        _db.SeedOrder(4, OrderStatus.Completed, 1);
        await using (var context = _db.CreateContext())
        {
            context.ShoppingCarts.Add(new ShoppingCart { UserId = 3 });
            context.UserSnapshots.Add(new UserSnapshot { UserId = 3, UserName = "trinity" });
            await context.SaveChangesAsync();
        }

        await ConsumeAsync(new UserChanged { UserId = 3, UserName = "trinity", Email = "", IsDeleted = true });

        await using var check = _db.CreateContext();
        Assert.Equal(0, await check.Orders.CountAsync(o => o.UserId == 3));
        Assert.Equal(0, await check.OrderMovies.CountAsync(om => om.Order.UserId == 3));
        Assert.Equal(0, await check.ShoppingCarts.CountAsync());
        Assert.Equal(0, await check.UserSnapshots.CountAsync());
        Assert.Equal(1, await check.Orders.CountAsync(o => o.UserId == 4));
    }

    public void Dispose() => _db.Dispose();
}
