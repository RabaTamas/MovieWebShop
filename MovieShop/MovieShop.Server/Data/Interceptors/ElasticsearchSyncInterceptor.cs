using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Data.Interceptors
{
    /// <summary>
    /// EF Core interceptor that automatically syncs Movie changes to Elasticsearch
    /// after every successful SaveChanges call.
    /// </summary>
    public class ElasticsearchSyncInterceptor : SaveChangesInterceptor
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public ElasticsearchSyncInterceptor(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context == null) return result;

            // Collect IDs of changed Movie entities
            var changedMovies = context.ChangeTracker.Entries<Movie>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => (e.Entity.Id, IsDeleted: e.State == EntityState.Deleted || e.Entity.IsDeleted))
                .ToList();

            if (!changedMovies.Any()) return result;

            // Use a new scope to avoid using a disposed DbContext
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var es = scope.ServiceProvider.GetRequiredService<IElasticsearchService>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
                        .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

                    if (movie != null)
                        await es.IndexMovieAsync(movie);
                }
            }, cancellationToken);

            return result;
        }
    }
}
