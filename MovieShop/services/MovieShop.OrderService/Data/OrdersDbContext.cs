using Microsoft.EntityFrameworkCore;
using MovieShop.OrderService.Models;

namespace MovieShop.OrderService.Data;

/// <summary>
/// Az Order Service saját adatbázisa (MovieShop.Orders).
///
/// Birtokolt adat: Orders, OrderMovies, ShoppingCarts, ShoppingCartMovies
/// Projekciók:     MovieSnapshots, UserSnapshots
/// </summary>
public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderMovie> OrderMovies => Set<OrderMovie>();
    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();
    public DbSet<ShoppingCartMovie> ShoppingCartMovies => Set<ShoppingCartMovie>();

    public DbSet<MovieSnapshot> MovieSnapshots => Set<MovieSnapshot>();
    public DbSet<UserSnapshot> UserSnapshots => Set<UserSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrderMovie>()
            .HasKey(om => new { om.OrderId, om.MovieId });

        modelBuilder.Entity<OrderMovie>()
            .HasOne(om => om.Order)
            .WithMany(o => o.OrderMovies)
            .HasForeignKey(om => om.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShoppingCartMovie>()
            .HasKey(scm => new { scm.ShoppingCartId, scm.MovieId });

        modelBuilder.Entity<ShoppingCartMovie>()
            .HasOne(scm => scm.ShoppingCart)
            .WithMany(c => c.ShoppingCartMovies)
            .HasForeignKey(scm => scm.ShoppingCartId)
            .OnDelete(DeleteBehavior.Cascade);

        // Felhasználónként egyetlen aktív kosár
        modelBuilder.Entity<ShoppingCart>()
            .HasIndex(c => c.UserId)
            .IsUnique();

        // A rendeléslekérdezések mindig felhasználóra és állapotra szűrnek
        modelBuilder.Entity<Order>()
            .HasIndex(o => new { o.UserId, o.Status });

        // FIGYELEM: a MovieSnapshot és a OrderMovie.MovieId KÖZÖTT SZÁNDÉKOSAN
        // NINCS idegen kulcs. Egy rendelés akkor is érvényes marad, ha a film
        // snapshotja még nem érkezett meg, vagy már törölték a katalógusból.
        // A referenciális integritást itt az eseményfolyam biztosítja, nem az adatbázis.
    }
}
