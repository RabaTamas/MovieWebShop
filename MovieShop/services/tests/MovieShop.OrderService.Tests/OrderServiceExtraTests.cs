using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.OrderService.Models;
using MovieShop.OrderService.Services;

namespace MovieShop.OrderService.Tests;

public class OrderServiceExtraTests : IDisposable
{
    private readonly OrdersDb _db = new();

    private Services.OrderService Create(Data.OrdersDbContext? context = null)
    {
        context ??= _db.CreateContext();
        return new Services.OrderService(context, new ShoppingCartService(context, NullLogger<ShoppingCartService>.Instance),
            Substitute.For<IUserServiceClient>(), Substitute.For<IPublishEndpoint>(), NullLogger<Services.OrderService>.Instance);
    }

    [Fact]
    public async Task AdminQueries_AndStatusFilter()
    {
        var a = _db.SeedOrder(7, OrderStatus.Completed, 1);
        _db.SeedOrder(8, OrderStatus.Pending, 2);
        var service = Create();

        Assert.Equal(2, (await service.GetAllOrdersAsync()).Count);
        Assert.Equal(a, (await service.GetOrderByIdAdminAsync(a))!.Id);
        Assert.Null(await service.GetOrderByIdAdminAsync(999));
        Assert.Single(await service.GetOrdersByStatusAsync("Pending"));
        Assert.Empty(await service.GetOrdersByStatusAsync("Shipped"));
        Assert.Null(await service.GetOrderByIdAsync(a, 8));
        Assert.Empty(await service.GetOrdersByUserIdAsync(99));
    }

    [Fact]
    public async Task Statistics_SummarizeRevenueStatusesAndTopSellers()
    {
        _db.SeedMovie(1, "The Matrix", 999);
        _db.SeedOrder(7, OrderStatus.Completed, 1, 2);
        _db.SeedOrder(8, OrderStatus.Pending, 1);

        var stats = await Create().GetOrderStatisticsAsync();

        Assert.Equal(2, stats.TotalOrders);
        Assert.Equal(2, stats.OrdersToday);
        Assert.Equal(1, stats.OrdersByStatus["Completed"]);
        Assert.Equal(1, stats.OrdersByStatus["Pending"]);
        Assert.Equal("The Matrix", stats.TopSellingMovies[0].Title);
        Assert.Equal(2, stats.TopSellingMovies[0].Quantity);
        Assert.Contains(stats.TopSellingMovies, m => m.Title == "Unknown movie");
    }

    [Fact]
    public async Task UpdateStatus_UnknownOrder_AndDatabaseFailure_ReturnFalse()
    {
        Assert.False(await Create().UpdateOrderStatusAsync(999, "Completed"));

        var broken = _db.CreateContext();
        await broken.DisposeAsync();
        Assert.False(await Create(broken).UpdateOrderStatusAsync(1, "Completed"));
    }

    [Fact]
    public async Task UpdateStatus_CompletedToCancelled_WhenMovieOwnedElsewhere_PublishesNothing()
    {
        _db.SeedOrder(7, OrderStatus.Completed, 1);
        var orderId = _db.SeedOrder(7, OrderStatus.Completed, 1);
        var publish = Substitute.For<IPublishEndpoint>();
        var context = _db.CreateContext();
        var service = new Services.OrderService(context, new ShoppingCartService(context, NullLogger<ShoppingCartService>.Instance),
            Substitute.For<IUserServiceClient>(), publish, NullLogger<Services.OrderService>.Instance);

        Assert.True(await service.UpdateOrderStatusAsync(orderId, "Cancelled"));
        await publish.DidNotReceiveWithAnyArgs().Publish<MovieShop.Contracts.Events.OrderRevoked>(default!, default);
    }

    public void Dispose() => _db.Dispose();
}

public class ShoppingCartExtraTests : IDisposable
{
    private readonly OrdersDb _db = new();

    private ShoppingCartService Create() => new(_db.CreateContext(), NullLogger<ShoppingCartService>.Instance);

    [Fact]
    public async Task UpdateAndRemove_WithoutCartOrItem_ReturnFalse()
    {
        _db.SeedMovie(1, "Film", 1000);
        var service = Create();

        Assert.False(await service.UpdateCartItemQuantityAsync(7, 1, 2));
        Assert.False(await service.RemoveFromCartAsync(7, 1));
        Assert.False(await service.ClearCartAsync(7));

        await service.AddToCartAsync(7, 1, 1);
        Assert.False(await Create().UpdateCartItemQuantityAsync(7, 99, 2));
        Assert.False(await Create().RemoveFromCartAsync(7, 99));
        Assert.True(await Create().UpdateCartItemQuantityAsync(7, 1, 4));
        Assert.Equal(4, (await Create().GetCartByUserIdAsync(7)).Items.Single().Quantity);
        Assert.True(await Create().RemoveFromCartAsync(7, 1));
    }

    [Fact]
    public async Task AddToCart_DatabaseFailure_ReturnsFalse()
    {
        var broken = _db.CreateContext();
        await broken.DisposeAsync();

        Assert.False(await new ShoppingCartService(broken, NullLogger<ShoppingCartService>.Instance).AddToCartAsync(7, 1, 1));
    }

    public void Dispose() => _db.Dispose();
}

public class RecommendationContextTests : IDisposable
{
    private readonly OrdersDb _db = new();

    [Fact]
    public async Task Context_DescribesCategoryAndCollaborativeSuggestions()
    {
        _db.SeedMovie(1, "The Matrix", 999, categories: "Action");
        _db.SeedMovie(2, "John Wick", 1500, discountedPrice: 1200, categories: "Action");
        _db.SeedMovie(3, "Interstellar", 1300, categories: "Drama");
        _db.SeedOrder(7, OrderStatus.Completed, 1);
        _db.SeedOrder(8, OrderStatus.Completed, 1, 3);

        var text = await new RecommendationService(_db.CreateContext()).GetRecommendationContextAsync(7);

        Assert.Contains("=== PERSONALIZED RECOMMENDATIONS ===", text);
        Assert.Contains("John Wick (Action) - 1200 Ft", text);
        Assert.Contains("Customers with similar taste also bought:", text);
        Assert.Contains("Interstellar", text);
    }

    [Fact]
    public async Task Context_IsEmptyWithoutPurchases_AndCategoryless()
    {
        _db.SeedMovie(1, "Kategória nélkül", 999);
        _db.SeedOrder(8, OrderStatus.Completed, 1);

        Assert.Equal(string.Empty, await new RecommendationService(_db.CreateContext()).GetRecommendationContextAsync(7));
        Assert.Empty((await new RecommendationService(_db.CreateContext()).GetRecommendationsAsync(8)).CategoryBased);
    }

    public void Dispose() => _db.Dispose();
}
