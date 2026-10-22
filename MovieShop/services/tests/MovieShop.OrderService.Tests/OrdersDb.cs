using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MovieShop.OrderService.Data;
using MovieShop.OrderService.Models;

namespace MovieShop.OrderService.Tests;

/// <summary>
/// Memóriában futó SQLite adatbázis. Az EF InMemory providerrel szemben valódi
/// tranzakciókat és NOT NULL / egyediségi kényszereket ad, ami a rendelés
/// visszagörgetésének és a kompenzációnak a teszteléséhez kell.
/// </summary>
public sealed class OrdersDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public OrdersDb()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public OrdersDbContext CreateContext()
        => new(new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options);

    public void SeedMovie(int movieId, string title, int price, int? discountedPrice = null,
        string categories = "", bool isDeleted = false)
    {
        using var context = CreateContext();
        context.MovieSnapshots.Add(new MovieSnapshot
        {
            MovieId = movieId,
            Title = title,
            Price = price,
            DiscountedPrice = discountedPrice,
            CategoriesCsv = categories,
            IsDeleted = isDeleted
        });
        context.SaveChanges();
    }

    public int SeedOrder(int userId, OrderStatus status, params int[] movieIds)
    {
        using var context = CreateContext();
        var order = new Order
        {
            UserId = userId,
            Status = status.ToString(),
            BillingStreet = "Fő utca 1.",
            BillingCity = "Budapest",
            BillingZip = "1111",
            OrderMovies = movieIds.Select(id => new OrderMovie { MovieId = id, PriceAtOrder = 1000 }).ToList()
        };
        context.Orders.Add(order);
        context.SaveChanges();
        return order.Id;
    }

    public void Dispose() => _connection.Dispose();
}
