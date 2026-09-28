using Microsoft.EntityFrameworkCore;
using MovieShop.AuctionService.Models;

namespace MovieShop.AuctionService.Data;

/// <summary>
/// Az Auction Service saját adatbázisa (MovieShop.Auctions).
///
/// Birtokolt adat: Auctions, Bids
/// Projekciók:     MovieSnapshots, UserSnapshots
/// </summary>
public class AuctionsDbContext : DbContext
{
    public AuctionsDbContext(DbContextOptions<AuctionsDbContext> options) : base(options) { }

    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<Bid> Bids => Set<Bid>();

    public DbSet<MovieSnapshot> MovieSnapshots => Set<MovieSnapshot>();
    public DbSet<UserSnapshot> UserSnapshots => Set<UserSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Bid>()
            .HasOne(b => b.Auction)
            .WithMany(a => a.Bids)
            .HasForeignKey(b => b.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        // A licitlista mindig aukcióra szűr, időrend szerint csökkenő sorrendben
        modelBuilder.Entity<Bid>()
            .HasIndex(b => new { b.AuctionId, b.PlacedAt });

        // Az aktív aukciók listája a leggyakoribb lekérdezés
        modelBuilder.Entity<Auction>()
            .HasIndex(a => new { a.Status, a.EndsAt });

        // A nyertes saját aukcióinak lekérdezéséhez (/my-wins oldal)
        modelBuilder.Entity<Auction>()
            .HasIndex(a => a.CurrentBidderId);
    }
}
