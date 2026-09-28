using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MovieShop.UserService.Models;

namespace MovieShop.UserService.Data;

/// <summary>
/// A User Service saját adatbázisa (MovieShop.Users). Kizárólag az ASP.NET Core Identity
/// tábláit és a címeket tartalmazza — filmet, rendelést, kosarat nem lát.
///
/// A monolit AppDbContext-jéhez képest 11 entitásból 2 maradt.
/// </summary>
public class UsersDbContext : IdentityDbContext<User, IdentityRole<int>, int>
{
    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }

    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<WebPushSubscription> WebPushSubscriptions => Set<WebPushSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Address>()
            .HasOne(a => a.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Web Push: egy böngésző-feliratkozás (Endpoint) csak egyszer szerepelhet;
        // a felhasználó törlésével a feliratkozásai is törlődnek
        modelBuilder.Entity<WebPushSubscription>()
            .HasIndex(s => s.Endpoint)
            .IsUnique();

        modelBuilder.Entity<WebPushSubscription>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
