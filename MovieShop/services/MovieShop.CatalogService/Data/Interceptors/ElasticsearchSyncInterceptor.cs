using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MovieShop.CatalogService.Models;
using MovieShop.CatalogService.Services;

namespace MovieShop.CatalogService.Data.Interceptors;

/// <summary>
/// EF Core interceptor, amely minden sikeres SaveChanges után szinkronizálja a
/// megváltozott filmeket az Elasticsearch indexszel.
///
/// Ez a monolitból változtatás nélkül átvehető darab — épp azért, mert kizárólag a
/// Catalog saját adatára épül. Ha az Elasticsearch a service-határon kívülre került
/// volna, ez az interceptor nem működhetne így.
/// </summary>
public class ElasticsearchSyncInterceptor : SaveChangesInterceptor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ElasticsearchSyncInterceptor> _logger;

    public ElasticsearchSyncInterceptor(
        IServiceScopeFactory scopeFactory,
        ILogger<ElasticsearchSyncInterceptor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // A SavedChanges idején az EF már elfogadta a változásokat (minden entitás Unchanged),
    // ezért a megváltozott filmeket a mentés ELŐTT kell rögzíteni.
    private readonly ConditionalWeakTable<DbContext, List<(Movie Movie, bool Removed)>> _pending = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            var changed = context.ChangeTracker.Entries<Movie>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => (e.Entity, e.State == EntityState.Deleted))
                .ToList();

            _pending.AddOrUpdate(context, changed);
        }

        return ValueTask.FromResult(result);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
            _pending.Remove(context);

        return Task.CompletedTask;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null || !_pending.TryGetValue(context, out var pending))
            return ValueTask.FromResult(result);

        _pending.Remove(context);

        // Az új filmek azonosítója csak a mentés után ismert, ezért itt olvassuk ki
        var changedMovies = pending
            .Select(p => (p.Movie.Id, IsDeleted: p.Removed || p.Movie.IsDeleted))
            .ToList();

        if (changedMovies.Count == 0)
            return ValueTask.FromResult(result);

        // Külön scope, mert az eredeti DbContext a hívás után felszabadulhat.
        // Az indexelés szándékosan nem blokkolja a válaszadást: ha az Elasticsearch
        // lassú vagy nem elérhető, attól a mentés már sikeres.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var es = scope.ServiceProvider.GetRequiredService<IElasticsearchService>();
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

                foreach (var (id, isDeleted) in changedMovies)
                {
                    if (isDeleted)
                    {
                        await es.DeleteMovieFromIndexAsync(id);
                        continue;
                    }

                    var movie = await db.Movies
                        .Include(m => m.Categories)
                        .Include(m => m.Reviews)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Id == id, CancellationToken.None);

                    if (movie != null)
                        await es.IndexMovieAsync(movie);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Az Elasticsearch szinkronizáció sikertelen");
            }
        }, CancellationToken.None);

        return ValueTask.FromResult(result);
    }
}
