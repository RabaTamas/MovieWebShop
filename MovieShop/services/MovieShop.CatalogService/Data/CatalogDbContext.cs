using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Models;

namespace MovieShop.CatalogService.Data;

/// <summary>
/// A Catalog Service saját adatbázisa (MovieShop.Catalog).
///
/// Két csoportra oszlik:
///  - Birtokolt adat: Movies, Categories, Reviews, VideoProgresses
///  - Projekciók más service-ek eseményeiből: Entitlements, UserSnapshots
///
/// A projekciós táblákat üzleti kód soha nem írja, csak az eseménykezelők.
/// </summary>
public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<VideoProgress> VideoProgresses => Set<VideoProgress>();

    // ── Projekciók (read model) ──────────────────────────────────────────────
    public DbSet<Entitlement> Entitlements => Set<Entitlement>();
    public DbSet<UserSnapshot> UserSnapshots => Set<UserSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Movie>()
            .HasMany(m => m.Categories)
            .WithMany(c => c.Movies)
            .UsingEntity<Dictionary<string, object>>(
                "MovieCategory",
                j => j.HasOne<Category>().WithMany().HasForeignKey("CategoryId").OnDelete(DeleteBehavior.Restrict),
                j => j.HasOne<Movie>().WithMany().HasForeignKey("MovieId").OnDelete(DeleteBehavior.Restrict));

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Movie)
            .WithMany(m => m.Reviews)
            .HasForeignKey(r => r.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Movie>()
            .Property(m => m.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // Egy felhasználó–film párhoz egyetlen pozíciórekord tartozhat
        modelBuilder.Entity<VideoProgress>()
            .HasIndex(vp => new { vp.UserId, vp.MovieId })
            .IsUnique();

        // A jogosultság-ellenőrzés a lejátszás forró útvonala, ezért fedő index kell rá.
        // Az egyediség egyben idempotenssé teszi az eseményfeldolgozást: ha ugyanaz az
        // OrderCompleted kétszer érkezik meg, a második beszúrás ütközik és eldobható.
        modelBuilder.Entity<Entitlement>()
            .HasIndex(e => new { e.UserId, e.MovieId })
            .IsUnique();
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<Movie>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;

                    var originalIsDeleted = entry.OriginalValues.GetValue<bool>(nameof(Movie.IsDeleted));

                    if (entry.Entity.IsDeleted && !originalIsDeleted)
                        entry.Entity.DeletedAt = DateTime.UtcNow;
                    else if (!entry.Entity.IsDeleted && originalIsDeleted)
                        entry.Entity.DeletedAt = null;

                    break;
            }
        }
    }
}
