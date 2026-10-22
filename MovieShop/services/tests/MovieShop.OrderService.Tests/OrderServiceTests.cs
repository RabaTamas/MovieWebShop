using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.Contracts.Events;
using MovieShop.OrderService.DTOs;
using MovieShop.OrderService.Models;
using MovieShop.OrderService.Services;

namespace MovieShop.OrderService.Tests;

public class OrderServiceTests : IDisposable
{
    private const int UserId = 7;

    private readonly OrdersDb _db = new();
    private readonly IUserServiceClient _userService = Substitute.For<IUserServiceClient>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    public OrderServiceTests()
    {
        _userService.CreateAddressAsync(Arg.Any<AddressDto>())
            .Returns(call =>
            {
                var a = call.Arg<AddressDto>();
                return new AddressDto { Id = 55, Street = a.Street, City = a.City, Zip = a.Zip };
            });
    }

    private Services.OrderService CreateService()
    {
        var context = _db.CreateContext();
        var cart = new ShoppingCartService(context, NullLogger<ShoppingCartService>.Instance);
        return new Services.OrderService(context, cart, _userService, _publish, NullLogger<Services.OrderService>.Instance);
    }

    private async Task PutInCartAsync(params int[] movieIds)
    {
        var cart = new ShoppingCartService(_db.CreateContext(), NullLogger<ShoppingCartService>.Instance);
        foreach (var id in movieIds)
            await cart.AddToCartAsync(UserId, id, 1);
    }

    private static OrderRequestDto Request(string? paymentIntentId = "pi_test", string street = "Fő utca 1.")
        => new()
        {
            BillingAddress = new AddressDto { Street = street, City = "Budapest", Zip = "1111" },
            PaymentIntentId = paymentIntentId
        };

    [Fact]
    public async Task CreateOrder_EmptyCart_ReturnsNull_AndCreatesNoAddress()
    {
        var order = await CreateService().CreateOrderFromCartAsync(UserId, Request());

        Assert.Null(order);
        await _userService.DidNotReceive().CreateAddressAsync(Arg.Any<AddressDto>());
    }

    [Fact]
    public async Task CreateOrder_PaidOrder_IsCompleted_ClearsCart_AndPublishesOrderCompleted()
    {
        _db.SeedMovie(1, "The Matrix", price: 999, discountedPrice: 799);
        _db.SeedMovie(2, "Inception", price: 1299);
        await PutInCartAsync(1, 2);

        var order = await CreateService().CreateOrderFromCartAsync(UserId, Request());

        Assert.NotNull(order);
        Assert.Equal(nameof(OrderStatus.Completed), order.Status);
        Assert.Equal(799 + 1299, order.TotalPrice);
        Assert.Equal(2, order.Movies.Count);

        // A számlázási cím pillanatképként kerül a rendelésre
        Assert.Equal("Fő utca 1.", order.BillingAddress.Street);
        Assert.Equal(55, order.BillingAddress.Id);

        await using var context = _db.CreateContext();
        Assert.Equal(0, await context.ShoppingCartMovies.CountAsync());

        await _publish.Received(1).Publish(
            Arg.Is<OrderCompleted>(e => e.UserId == UserId
                                        && e.MovieIds.Count == 2
                                        && e.TotalPrice == 799 + 1299),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrder_WithoutPayment_IsPending_AndPublishesNothing()
    {
        _db.SeedMovie(1, "The Matrix", price: 999);
        await PutInCartAsync(1);

        var order = await CreateService().CreateOrderFromCartAsync(UserId, Request(paymentIntentId: null));

        Assert.NotNull(order);
        Assert.Equal(nameof(OrderStatus.Pending), order.Status);
        await _publish.DidNotReceive().Publish(Arg.Any<OrderCompleted>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOrder_AddressCreationFails_ReturnsNull_AndKeepsCart()
    {
        _db.SeedMovie(1, "The Matrix", price: 999);
        await PutInCartAsync(1);
        _userService.CreateAddressAsync(Arg.Any<AddressDto>()).Returns((AddressDto?)null);

        var order = await CreateService().CreateOrderFromCartAsync(UserId, Request());

        Assert.Null(order);
        await using var context = _db.CreateContext();
        Assert.Equal(0, await context.Orders.CountAsync());
        Assert.Equal(1, await context.ShoppingCartMovies.CountAsync());
    }

    [Fact]
    public async Task CreateOrder_SaveFails_RollsBack_AndCompensatesAddress()
    {
        _db.SeedMovie(1, "The Matrix", price: 999);
        await PutInCartAsync(1);

        // A User Service hibás (utca nélküli) címet ad vissza: a rendelés mentése a
        // NOT NULL kényszeren elbukik, a tranzakció visszagörgetődik
        _userService.CreateAddressAsync(Arg.Any<AddressDto>())
            .Returns(new AddressDto { Id = 99, Street = null!, City = "Budapest", Zip = "1111" });

        var order = await CreateService().CreateOrderFromCartAsync(UserId, Request());

        Assert.Null(order);

        // Saga-kompenzáció: a User Service-ben már létrehozott címet törölni kell
        await _userService.Received(1).DeleteAddressAsync(99);

        await using var context = _db.CreateContext();
        Assert.Equal(0, await context.Orders.CountAsync());
        Assert.Equal(1, await context.ShoppingCartMovies.CountAsync());
        await _publish.DidNotReceive().Publish(Arg.Any<OrderCompleted>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatus_PendingToCompleted_PublishesOrderCompleted()
    {
        var orderId = _db.SeedOrder(UserId, OrderStatus.Pending, 1, 2);

        var ok = await CreateService().UpdateOrderStatusAsync(orderId, nameof(OrderStatus.Completed));

        Assert.True(ok);
        await _publish.Received(1).Publish(
            Arg.Is<OrderCompleted>(e => e.OrderId == orderId && e.MovieIds.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatus_CompletedToRefunded_RevokesOnlyMoviesNotOwnedElsewhere()
    {
        // Az 1-es filmet a felhasználó egy másik teljesített rendelésben is megvette
        _db.SeedOrder(UserId, OrderStatus.Completed, 1);
        var orderId = _db.SeedOrder(UserId, OrderStatus.Completed, 1, 2);

        var ok = await CreateService().UpdateOrderStatusAsync(orderId, nameof(OrderStatus.Refunded));

        Assert.True(ok);
        await _publish.Received(1).Publish(
            Arg.Is<OrderRevoked>(e => e.OrderId == orderId
                                      && e.MovieIds.Count == 1
                                      && e.MovieIds[0] == 2
                                      && e.Reason == nameof(OrderStatus.Refunded)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatus_UnknownStatus_ReturnsFalse_AndKeepsOrder()
    {
        var orderId = _db.SeedOrder(UserId, OrderStatus.Pending, 1);

        var ok = await CreateService().UpdateOrderStatusAsync(orderId, "Shipped");

        Assert.False(ok);
        await using var context = _db.CreateContext();
        Assert.Equal(nameof(OrderStatus.Pending), (await context.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task PurchasedMovies_CountOnlyCompletedOrders()
    {
        _db.SeedOrder(UserId, OrderStatus.Completed, 1);
        _db.SeedOrder(UserId, OrderStatus.Pending, 2);
        _db.SeedOrder(UserId + 1, OrderStatus.Completed, 3);

        var service = CreateService();

        Assert.Equal([1], await service.GetPurchasedMovieIdsAsync(UserId));
        Assert.True(await service.HasUserPurchasedMovieAsync(UserId, 1));
        Assert.False(await service.HasUserPurchasedMovieAsync(UserId, 2));
    }

    [Fact]
    public async Task GetOrders_EnrichesTitlesAndUserNameFromSnapshots()
    {
        _db.SeedMovie(1, "The Matrix", price: 999);
        _db.SeedOrder(UserId, OrderStatus.Completed, 1, 404);

        await using (var context = _db.CreateContext())
        {
            context.UserSnapshots.Add(new UserSnapshot { UserId = UserId, UserName = "neo", Email = "neo@zion.io" });
            await context.SaveChangesAsync();
        }

        var order = Assert.Single(await CreateService().GetOrdersByUserIdAsync(UserId));

        Assert.Equal("neo", order.UserName);
        Assert.Contains(order.Movies, m => m.MovieId == 1 && m.Title == "The Matrix");
        Assert.Contains(order.Movies, m => m.MovieId == 404 && m.Title == "Unknown movie");
    }

    public void Dispose() => _db.Dispose();
}
