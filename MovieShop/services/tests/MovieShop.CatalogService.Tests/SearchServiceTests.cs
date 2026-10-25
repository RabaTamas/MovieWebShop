using System.Text;
using Elasticsearch.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Models;
using MovieShop.CatalogService.Services;
using Nest;

namespace MovieShop.CatalogService.Tests;

public class SqlSearchServiceTests
{
    private readonly CatalogDb _db = new();

    public SqlSearchServiceTests()
    {
        _db.AddMovie("The Matrix", 999, 799, categories: "Action");
        _db.AddMovie("Matrix Reloaded", 1500, categories: "Action");
        _db.AddMovie("Notebook", 600, categories: "Drama");
        _db.AddMovie("Deleted Matrix", 100, deleted: true, categories: "Action");
    }

    private SqlSearchService CreateService() => new(_db.Create(), NullLogger<SqlSearchService>.Instance);

    private async Task<List<string>> TitlesAsync(SearchRequestDto req)
        => (await CreateService().SearchAsync(req)).Movies.Select(m => m.Title).ToList();

    [Fact]
    public async Task Search_ByTerm_PrefersTitleMatches_AndHidesDeleted()
    {
        var result = await CreateService().SearchAsync(new SearchRequestDto { Q = " Matrix " });

        Assert.Equal(2, result.Total);
        Assert.Equal(["Matrix Reloaded", "The Matrix"], result.Movies.Select(m => m.Title));
        Assert.Equal(["Action"], result.Movies.First().Categories);
    }

    [Fact]
    public async Task Search_FiltersByCategoryAndEffectivePrice()
    {
        Assert.Equal(["Notebook"], await TitlesAsync(new SearchRequestDto { Categories = ["Drama"] }));
        // A The Matrix akciós ára (799) számít, nem az eredeti (999)
        Assert.Equal(["The Matrix"], (await TitlesAsync(new SearchRequestDto { MinPrice = 700, MaxPrice = 800 })));
        Assert.Empty(await TitlesAsync(new SearchRequestDto { MinRating = 3 }));
    }

    [Theory]
    [InlineData("price-asc", new[] { "Notebook", "The Matrix", "Matrix Reloaded" })]
    [InlineData("price-desc", new[] { "Matrix Reloaded", "The Matrix", "Notebook" })]
    [InlineData("name-asc", new[] { "Matrix Reloaded", "Notebook", "The Matrix" })]
    [InlineData("name-desc", new[] { "The Matrix", "Notebook", "Matrix Reloaded" })]
    public async Task Search_Sorts(string sort, string[] expected)
        => Assert.Equal(expected, await TitlesAsync(new SearchRequestDto { Sort = sort }));

    [Fact]
    public async Task Search_RatingSortAndDefaultOrder_AndPaging()
    {
        Assert.Equal(3, (await TitlesAsync(new SearchRequestDto { Sort = "rating-desc" })).Count);
        Assert.Equal(3, (await TitlesAsync(new SearchRequestDto())).Count);

        var page = await CreateService().SearchAsync(new SearchRequestDto { Page = 0, Size = 0 });
        Assert.Equal(1, page.Page);
        Assert.Equal(8, page.Size);

        var second = await CreateService().SearchAsync(new SearchRequestDto { Sort = "name-asc", Page = 2, Size = 2 });
        Assert.Equal(["The Matrix"], second.Movies.Select(m => m.Title));
        Assert.Equal(3, second.Total);
    }

    [Fact]
    public async Task Autocomplete_UsesTitlePrefix()
    {
        var service = CreateService();

        Assert.Equal(["Matrix Reloaded"], await service.AutocompleteAsync("Matr"));
        Assert.Empty(await service.AutocompleteAsync("  "));
    }

    [Fact]
    public async Task IndexOperations_AreNoOps()
    {
        var service = CreateService();
        await service.IndexMovieAsync(new Movie());
        await service.DeleteMovieFromIndexAsync(1);
        await service.ReindexAllAsync();
        await service.EnsureIndexExistsAsync();
    }
}

/// <summary>
/// Az Elasticsearch-ág a NEST memóriabeli kapcsolatával fut: a lekérdezések valóban
/// felépülnek és JSON-ná szerializálódnak, a választ pedig előre megadott tartalom adja.
/// </summary>
public class ElasticsearchServiceTests
{
    private const string SearchResponse = """
        {"took":1,"timed_out":false,"hits":{"total":{"value":1,"relation":"eq"},"hits":[
          {"_index":"movies","_id":"1","_score":1.0,"_source":{"id":1,"title":"The Matrix","description":"d","price":999,
           "discountedPrice":799,"imageUrl":"m.jpg","categories":["Action"],"averageRating":0,"reviewCount":2}}]}}
        """;

    private readonly List<string> _requests = [];

    private ElasticsearchService CreateService(string response, int status = 200, IServiceScopeFactory? scopes = null)
    {
        var connection = new RecordingConnection(Encoding.UTF8.GetBytes(response), status, _requests);
        var settings = new ConnectionSettings(new SingleNodeConnectionPool(new Uri("http://es:9200")), connection)
            .DisableDirectStreaming();
        return new ElasticsearchService(new ElasticClient(settings), scopes ?? Substitute.For<IServiceScopeFactory>(),
            NullLogger<ElasticsearchService>.Instance);
    }

    [Fact]
    public async Task Search_MapsHits_AndBuildsFilteredFuzzyQuery()
    {
        var result = await CreateService(SearchResponse).SearchAsync(new SearchRequestDto
        {
            Q = "matrx", Categories = ["Action"], MinPrice = 500, MaxPrice = 900, MinRating = 1, Sort = "price-asc", Page = 2, Size = 5
        });

        var movie = Assert.Single(result.Movies);
        Assert.Equal("The Matrix", movie.Title);
        Assert.Equal(799, movie.DiscountedPrice);
        Assert.Equal(1, result.Total);

        var body = _requests.Single();
        Assert.Contains("\"fuzziness\":\"AUTO\"", body);
        Assert.Contains("\"from\":5", body);
        Assert.Contains("\"terms\"", body);
        Assert.Contains("discountedPrice", body);
        Assert.Contains("averageRating", body);
    }

    [Theory]
    [InlineData("price-desc", "\"price\":{\"order\":\"desc\"}")]
    [InlineData("name-asc", "title.keyword")]
    [InlineData("name-desc", "title.keyword")]
    [InlineData("rating-desc", "averageRating")]
    [InlineData(null, "_score")]
    public async Task Search_SortsAndFilterOnlyQueries(string? sort, string expectedInBody)
    {
        await CreateService(SearchResponse).SearchAsync(new SearchRequestDto { Sort = sort, Categories = ["Drama"] });
        await CreateService(SearchResponse).SearchAsync(new SearchRequestDto { Sort = sort });

        Assert.Contains(expectedInBody, _requests[0]);
        Assert.Contains("match_all", _requests[1]);
    }

    [Fact]
    public async Task Autocomplete_ReturnsDistinctTitles_AndIgnoresBlank()
    {
        var service = CreateService(SearchResponse);

        Assert.Equal(["The Matrix"], await service.AutocompleteAsync("mat"));
        Assert.Empty(await service.AutocompleteAsync(" "));
        Assert.Contains("match_phrase_prefix", _requests.Single());
    }

    [Fact]
    public async Task IndexAndDelete_TargetTheMoviesIndex()
    {
        var service = CreateService("""{"result":"created","_id":"1","_index":"movies"}""");

        await service.IndexMovieAsync(new Movie { Id = 1, Title = "M", Categories = [new Category { Name = "Action" }] });
        await service.IndexMovieAsync(new Movie { Id = 2, IsDeleted = true });

        Assert.Contains("\"title\":\"M\"", _requests[0]);
        Assert.Equal(2, _requests.Count);
    }

    [Fact]
    public async Task EnsureIndex_CreatesWhenMissing_AndSwallowsErrors()
    {
        await CreateService("{}", status: 404).EnsureIndexExistsAsync();
        Assert.Equal(2, _requests.Count); // exists + create

        await CreateService("{}", status: 200).EnsureIndexExistsAsync();
        Assert.Equal(3, _requests.Count); // csak exists

        var throwing = new ElasticsearchService(Substitute.For<IElasticClient>(), Substitute.For<IServiceScopeFactory>(),
            NullLogger<ElasticsearchService>.Instance);
        await throwing.EnsureIndexExistsAsync(); // a helyettesített kliens null választ ad → elkapott hiba
    }

    [Fact]
    public async Task ReindexAll_BulkIndexesActiveMovies()
    {
        var db = new CatalogDb();
        db.AddMovie("Aktív", categories: "Action");
        db.AddMovie("Törölt", deleted: true);

        var services = new ServiceCollection();
        services.AddScoped(_ => db.Create());
        var provider = services.BuildServiceProvider();

        await CreateService("{}", scopes: provider.GetRequiredService<IServiceScopeFactory>()).ReindexAllAsync();

        var bulk = _requests.Last();
        Assert.Contains("Aktív", bulk);
        Assert.DoesNotContain("Törölt", bulk);
    }

    [Fact]
    public async Task ReindexAll_WithNoMovies_SkipsBulk()
    {
        var services = new ServiceCollection();
        var db = new CatalogDb();
        services.AddScoped(_ => db.Create());
        var provider = services.BuildServiceProvider();

        await CreateService("{}", scopes: provider.GetRequiredService<IServiceScopeFactory>()).ReindexAllAsync();

        Assert.DoesNotContain(_requests, r => r.Contains("\"index\""));
    }

    /// <summary>A kéréstörzseket rögzítő memóriabeli NEST-kapcsolat.</summary>
    private sealed class RecordingConnection(byte[] response, int status, List<string> requests)
        : InMemoryConnection(response, status)
    {
        public override TResponse Request<TResponse>(RequestData requestData)
        {
            Record(requestData);
            return base.Request<TResponse>(requestData);
        }

        public override Task<TResponse> RequestAsync<TResponse>(RequestData requestData, CancellationToken cancellationToken)
        {
            Record(requestData);
            return base.RequestAsync<TResponse>(requestData, cancellationToken);
        }

        private void Record(RequestData data)
        {
            // A NEST 7 az első kérés előtt egy termék-ellenőrző GET /-et küld – ez nem a mi kérésünk
            if (data.Uri.AbsolutePath == "/") return;

            using var ms = new MemoryStream();
            data.PostData?.Write(ms, data.ConnectionSettings);
            requests.Add($"{data.Method} {data.Uri} {Encoding.UTF8.GetString(ms.ToArray())}");
        }
    }
}
