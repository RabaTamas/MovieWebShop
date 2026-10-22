using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.OrderService.Services;

namespace MovieShop.OrderService.Tests;

public class ShoppingCartServiceTests : IDisposable
{
    private const int UserId = 7;
    private readonly OrdersDb _db = new();

    private ShoppingCartService CreateService()
        => new(_db.CreateContext(), NullLogger<ShoppingCartService>.Instance);

    [Fact]
    public async Task AddToCart_UsesDiscountedPriceFromSnapshot()
    {
        _db.SeedMovie(1, "The Matrix", price: 999, discountedPrice: 799);

        var added = await CreateService().AddToCartAsync(UserId, 1, 1);
        var cart = await CreateService().GetCartByUserIdAsync(UserId);

        Assert.True(added);
        var item = Assert.Single(cart.Items);
        Assert.Equal("The Matrix", item.Title);
        Assert.Equal(799, item.PriceAtOrder);
    }

    [Fact]
    public async Task AddToCart_MovieWithoutSnapshot_IsRejected()
    {
        // A MovieChanged esemény még nem érkezett meg: a film nem tehető kosárba
        var added = await CreateService().AddToCartAsync(UserId, 42, 1);

        Assert.False(added);
        Assert.Empty((await CreateService().GetCartByUserIdAsync(UserId)).Items);
    }

    [Fact]
    public async Task AddToCart_SameMovieTwice_IncreasesQuantity()
    {
        _db.SeedMovie(1, "The Matrix", price: 999);

        await CreateService().AddToCartAsync(UserId, 1, 1);
        await CreateService().AddToCartAsync(UserId, 1, 1);

        var item = Assert.Single((await CreateService().GetCartByUserIdAsync(UserId)).Items);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public async Task UpdateQuantity_ToZero_RemovesItem()
    {
        _db.SeedMovie(1, "The Matrix", price: 999);
        await CreateService().AddToCartAsync(UserId, 1, 1);

        var updated = await CreateService().UpdateCartItemQuantityAsync(UserId, 1, 0);

        Assert.True(updated);
        Assert.Empty((await CreateService().GetCartByUserIdAsync(UserId)).Items);
    }

    [Fact]
    public async Task ClearCart_RemovesAllItemsButKeepsCart()
    {
        _db.SeedMovie(1, "The Matrix", price: 999);
        _db.SeedMovie(2, "Inception", price: 1299);
        await CreateService().AddToCartAsync(UserId, 1, 1);
        await CreateService().AddToCartAsync(UserId, 2, 1);

        var cleared = await CreateService().ClearCartAsync(UserId);

        Assert.True(cleared);
        await using var context = _db.CreateContext();
        Assert.Equal(0, await context.ShoppingCartMovies.CountAsync());
        Assert.Equal(1, await context.ShoppingCarts.CountAsync(c => c.UserId == UserId));
    }

    [Fact]
    public async Task Carts_AreIsolatedPerUser()
    {
        _db.SeedMovie(1, "The Matrix", price: 999);
        await CreateService().AddToCartAsync(UserId, 1, 1);

        var otherCart = await CreateService().GetCartByUserIdAsync(UserId + 1);

        Assert.Empty(otherCart.Items);
    }

    public void Dispose() => _db.Dispose();
}
