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

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null)
            return ValueTask.FromResult(result);

        var changedMovies = context.ChangeTracker.Entries<Movie>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => (e.Entity.Id, IsDeleted: e.State == EntityState.Deleted || e.Entity.IsDeleted))
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
