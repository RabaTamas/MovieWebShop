using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using MovieShop.Server.Models.Search;
using MovieShop.Server.Services.Interfaces;
using Nest;

namespace MovieShop.Server.Services.Implementations
{
    public class ElasticsearchService : IElasticsearchService
    {
        private readonly IElasticClient _client;
        private readonly IServiceScopeFactory _scopeFactory;
        private const string IndexName = "movies";

        public ElasticsearchService(IElasticClient client, IServiceScopeFactory scopeFactory)
        {
            _client = client;
            _scopeFactory = scopeFactory;
            EnsureIndexExistsAsync().GetAwaiter().GetResult();
        }

        private async Task EnsureIndexExistsAsync()
        {
            var exists = await _client.Indices.ExistsAsync(IndexName);
            if (exists.Exists) return;

            await _client.Indices.CreateAsync(IndexName, c => c
                .Map<MovieDocument>(m => m.AutoMap())
                .Settings(s => s
                    .NumberOfShards(1)
                    .NumberOfReplicas(0)));
        }

        public async Task IndexMovieAsync(Movie movie)
        {
            if (movie.IsDeleted) { await DeleteMovieFromIndexAsync(movie.Id); return; }

            var doc = ToDocument(movie);
            await _client.IndexAsync(doc, i => i.Index(IndexName).Id(movie.Id));
        }

        public async Task DeleteMovieFromIndexAsync(int movieId)
        {
            await _client.DeleteAsync<MovieDocument>(movieId, d => d.Index(IndexName));
        }

        public async Task<SearchResultDto> SearchAsync(SearchRequestDto req)
        {
            var from = (req.Page - 1) * req.Size;

            var response = await _client.SearchAsync<MovieDocument>(s => s
                .Index(IndexName)
                .From(from)
                .Size(req.Size)
                .Query(q => BuildQuery(q, req))
                .Sort(so => BuildSort(so, req.Sort)));

            return new SearchResultDto
            {
                Movies = response.Hits.Select(h => ToDto(h.Source)),
                Total = response.Total,
                Page = req.Page,
                Size = req.Size
            };
        }

        public async Task<IEnumerable<string>> AutocompleteAsync(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return [];

            var response = await _client.SearchAsync<MovieDocument>(s => s
                .Index(IndexName)
                .Size(6)
                .Source(src => src.Includes(i => i.Fields(f => f.Title)))
                .Query(q => q
                    .MatchPhrasePrefix(m => m
                        .Field(f => f.Title)
                        .Query(prefix)
                        .MaxExpansions(10))));

            return response.Hits
                .Select(h => h.Source.Title)
                .Distinct();
        }

        public async Task ReindexAllAsync()
        {
            // Delete and recreate index
            await _client.Indices.DeleteAsync(IndexName);
            await EnsureIndexExistsAsync();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var movies = await db.Movies
                .Include(m => m.Categories)
                .Include(m => m.Reviews)
                .Where(m => !m.IsDeleted)
                .ToListAsync();

            if (!movies.Any()) return;

            var docs = movies.Select(ToDocument).ToList();
            await _client.BulkAsync(b => b.Index(IndexName).IndexMany(docs));
        }

        // ── helpers ──────────────────────────────────────────────────

        private static QueryContainer BuildQuery(QueryContainerDescriptor<MovieDocument> q, SearchRequestDto req)
        {
            var filters = new List<QueryContainer>();

            // Category filter
            if (req.Categories?.Length > 0)
                filters.Add(q.Terms(t => t.Field(f => f.Categories).Terms(req.Categories)));

            // Price range filter (use discountedPrice if available, else price)
            if (req.MinPrice.HasValue || req.MaxPrice.HasValue)
            {
                filters.Add(q.Bool(b => b.Should(
                    s => s.Range(r => r.Field(f => f.DiscountedPrice)
                        .GreaterThanOrEquals((double?)req.MinPrice)
                        .LessThanOrEquals((double?)req.MaxPrice)),
                    s => s.Bool(bb => bb
                        .MustNot(mn => mn.Exists(e => e.Field(f => f.DiscountedPrice)))
                        .Filter(f => f.Range(r => r.Field(ff => ff.Price)
                            .GreaterThanOrEquals((double?)req.MinPrice)
                            .LessThanOrEquals((double?)req.MaxPrice))))
                ).MinimumShouldMatch(1)));
            }

            // Minimum rating filter
            if (req.MinRating.HasValue)
                filters.Add(q.Range(r => r.Field(f => f.AverageRating).GreaterThanOrEquals(req.MinRating)));

            // Full-text search with fuzzy matching
            if (!string.IsNullOrWhiteSpace(req.Q))
            {
                var textQuery = q.MultiMatch(m => m
                    .Fields(f => f
                        .Field(ff => ff.Title, boost: 3)
                        .Field(ff => ff.Description))
                    .Query(req.Q)
                    .Type(TextQueryType.BestFields)
                    .Fuzziness(Fuzziness.Auto));

                return filters.Count > 0
                    ? q.Bool(b => b.Must(textQuery).Filter(filters.ToArray()))
                    : textQuery;
            }

            return filters.Count > 0
                ? q.Bool(b => b.Filter(filters.ToArray()))
                : q.MatchAll();
        }

        private static IPromise<IList<ISort>> BuildSort(SortDescriptor<MovieDocument> s, string? sort) =>
            sort switch
            {
                "price-asc"   => s.Ascending(f => f.Price),
                "price-desc"  => s.Descending(f => f.Price),
                "name-asc"    => s.Field(f => f.Field(ff => ff.Title.Suffix("keyword")).Ascending()),
                "name-desc"   => s.Field(f => f.Field(ff => ff.Title.Suffix("keyword")).Descending()),
                "rating-desc" => s.Descending(f => f.AverageRating),
                _             => s.Descending(SortSpecialField.Score)
            };

        private static MovieDocument ToDocument(Movie movie) => new()
        {
            Id = movie.Id,
            Title = movie.Title,
            Description = movie.Description,
            Price = movie.Price,
            DiscountedPrice = movie.DiscountedPrice,
            ImageUrl = movie.ImageUrl,
            Categories = movie.Categories?.Select(c => c.Name).ToArray() ?? [],
            AverageRating = 0, // Review model has no Rating field
            ReviewCount = movie.Reviews?.Count ?? 0
        };

        private static MovieSearchItemDto ToDto(MovieDocument doc) => new()
        {
            Id = doc.Id,
            Title = doc.Title,
            Description = doc.Description,
            Price = doc.Price,
            DiscountedPrice = doc.DiscountedPrice,
            ImageUrl = doc.ImageUrl,
            Categories = doc.Categories,
            AverageRating = doc.AverageRating,
            ReviewCount = doc.ReviewCount
        };
    }
}
