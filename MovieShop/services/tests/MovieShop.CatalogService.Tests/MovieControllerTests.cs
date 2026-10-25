using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MovieShop.CatalogService.Controllers;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Models;
using MovieShop.CatalogService.Services;

namespace MovieShop.CatalogService.Tests;

public class MovieControllerTests
{
    private const int User = 7;

    private readonly CatalogDb _db = new();
    private readonly IMovieService _movies = Substitute.For<IMovieService>();
    private readonly IEntitlementService _entitlements = Substitute.For<IEntitlementService>();
    private readonly ITmdbService _tmdb = Substitute.For<ITmdbService>();
    private readonly IBlobStorageService _blob = Substitute.For<IBlobStorageService>();
    private readonly IHttpClientFactory _http = Substitute.For<IHttpClientFactory>();

    private MovieController Create(string? publicBaseUrl = "https://gw.test")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["PublicBaseUrl"] = publicBaseUrl })
            .Build();
        return new MovieController(_movies, _entitlements, _tmdb, _blob, _db.Create(), _http, config).As(User);
    }

    private void Owns(int movieId) => _entitlements.HasAccessAsync(User, movieId).Returns(true);

    private void Movie(int id, string? video = null, int? tmdbId = 603)
        => _movies.GetMovieByIdAsync(id).Returns(new MovieDetailsWithTmdbDto
        {
            Id = id, Title = "The Matrix", VideoFileName = video,
            TmdbInfo = tmdbId == null ? null : new TmdbMovieInfo { TmdbId = tmdbId.Value }
        });

    // ── Katalógus ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublicCatalogEndpoints_DelegateToService()
    {
        _movies.GetAllMoviesAsync().Returns([new MovieListDto { Id = 1 }]);
        _movies.GetMoviesByCategoryAsync(2).Returns([new MovieListDto { Id = 2 }]);
        _movies.GetMoviesByCategoriesAsync(Arg.Any<IEnumerable<int>>()).Returns([new MovieListDto { Id = 3 }]);
        Movie(1);
        var c = Create();

        Assert.IsType<OkObjectResult>((await c.GetAllMovies()).Result);
        Assert.IsType<OkObjectResult>((await c.GetMovie(1)).Result);
        Assert.IsType<NotFoundResult>((await c.GetMovie(99)).Result);
        Assert.IsType<OkObjectResult>((await c.GetMoviesByCategory(2)).Result);
        Assert.IsType<OkObjectResult>((await c.GetMoviesByCategories([1, 2])).Result);
        Assert.IsType<BadRequestObjectResult>((await c.GetMoviesByCategories([])).Result);
        Assert.IsType<BadRequestObjectResult>((await c.GetMoviesByCategories(null!)).Result);
    }

    [Fact]
    public async Task TmdbExtras_HandlesMissingMovieMissingTmdbAndMissingExtras()
    {
        Movie(1);
        Movie(2, tmdbId: null);
        _tmdb.GetMovieExtrasAsync(603).Returns(new TmdbMovieExtrasDto { Title = "Matrix" }, (TmdbMovieExtrasDto?)null);
        var c = Create();

        Assert.IsType<OkObjectResult>((await c.GetMovieTmdbExtras(1)).Result);
        Assert.IsType<NotFoundObjectResult>((await c.GetMovieTmdbExtras(1)).Result);
        Assert.IsType<NotFoundObjectResult>((await c.GetMovieTmdbExtras(2)).Result);
        Assert.IsType<NotFoundObjectResult>((await c.GetMovieTmdbExtras(99)).Result);
    }

    [Fact]
    public async Task AdminEndpoints_MapServiceResultsToStatusCodes()
    {
        _movies.GetAllMoviesForAdminAsync().Returns([]);
        _movies.GetDeletedMoviesAsync().Returns([]);
        _movies.GetMovieByIdForAdminAsync(1).Returns(new MovieDetailsDto { Id = 1 });
        _movies.AddMovieAsync(Arg.Is<MovieDetailsDto>(d => d.Title == "ok")).Returns(true);
        _movies.UpdateMovieAsync(1, Arg.Any<MovieDetailsDto>()).Returns(true);
        _movies.DeleteMovieAsync(1).Returns(true);
        _movies.RestoreMovieAsync(1).Returns(true);
        _movies.AddCategoryToMovieAsync(1, 2).Returns(true);
        _movies.RemoveCategoryFromMovieAsync(1, 2).Returns(true);
        var c = Create();

        Assert.IsType<OkObjectResult>((await c.GetAllMoviesForAdmin()).Result);
        Assert.IsType<OkObjectResult>((await c.GetDeletedMovies()).Result);
        Assert.IsType<OkObjectResult>((await c.GetMovieForAdmin(1)).Result);
        Assert.IsType<NotFoundResult>((await c.GetMovieForAdmin(2)).Result);

        Assert.IsType<CreatedAtActionResult>(await c.AddMovie(new MovieDetailsDto { Title = "ok" }));
        Assert.IsType<BadRequestObjectResult>(await c.AddMovie(new MovieDetailsDto { Title = "bad" }));

        Assert.IsType<NoContentResult>(await c.UpdateMovie(1, new MovieDetailsDto { Id = 1 }));
        Assert.IsType<BadRequestObjectResult>(await c.UpdateMovie(1, new MovieDetailsDto { Id = 2 }));
        Assert.IsType<NotFoundObjectResult>(await c.UpdateMovie(3, new MovieDetailsDto { Id = 3 }));

        Assert.IsType<OkObjectResult>(await c.DeleteMovie(1));
        Assert.IsType<NotFoundObjectResult>(await c.DeleteMovie(2));
        Assert.IsType<OkObjectResult>(await c.RestoreMovie(1));
        Assert.IsType<NotFoundObjectResult>(await c.RestoreMovie(2));

        Assert.IsType<NoContentResult>(await c.AddCategoryToMovie(1, 2));
        Assert.IsType<BadRequestObjectResult>(await c.AddCategoryToMovie(1, 3));
        Assert.IsType<NoContentResult>(await c.RemoveCategoryFromMovie(1, 2));
        Assert.IsType<BadRequestObjectResult>(await c.RemoveCategoryFromMovie(1, 3));
    }

    // ── Megvásárolt tartalom ──────────────────────────────────────────────────

    [Fact]
    public async Task Purchased_ReturnsOnlyEntitledMovies()
    {
        _entitlements.GetEntitledMovieIdsAsync(User).Returns([2], new List<int>());
        _movies.GetAllMoviesAsync().Returns([new MovieListDto { Id = 1 }, new MovieListDto { Id = 2 }]);
        var c = Create();

        var owned = (IEnumerable<MovieListDto>)((OkObjectResult)(await c.GetPurchasedMovies()).Result!).Value!;
        Assert.Equal([2], owned.Select(m => m.Id));

        var none = (IEnumerable<MovieListDto>)((OkObjectResult)(await c.GetPurchasedMovies()).Result!).Value!;
        Assert.Empty(none);
    }

    [Fact]
    public async Task Trailer_ChecksOwnershipTmdbAndErrors()
    {
        Owns(1); Owns(2); Owns(3);
        Movie(1);
        Movie(2, tmdbId: null);
        Movie(3, tmdbId: 700);
        _tmdb.GetTrailerAsync(603).Returns(new TmdbVideoDto { Key = "abc", Name = "Trailer" }, (TmdbVideoDto?)null);
        _tmdb.GetTrailerAsync(700).Returns<TmdbVideoDto?>(_ => throw new HttpRequestException("TMDB down"));
        var c = Create();

        var ok = (MovieTrailerDto)((OkObjectResult)(await c.GetMovieTrailer(1)).Result!).Value!;
        Assert.Equal("https://www.youtube.com/embed/abc", ok.Url);
        Assert.IsType<NotFoundObjectResult>((await c.GetMovieTrailer(1)).Result);
        Assert.IsType<NotFoundObjectResult>((await c.GetMovieTrailer(2)).Result);
        Assert.Equal(500, ((ObjectResult)(await c.GetMovieTrailer(3)).Result!).StatusCode);
        Assert.Equal(403, ((ObjectResult)(await c.GetMovieTrailer(4)).Result!).StatusCode);
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Stream_WithoutPurchase_Is403_AndMissingVideoIs404()
    {
        Owns(2); Owns(3); Owns(4);
        Movie(2, video: null);
        Movie(3, video: "3_master.m3u8");
        var c = Create();

        Assert.Equal(403, (await c.GetStreamingUrl(1)).Status());
        Assert.Equal(404, (await c.GetStreamingUrl(2)).Status());
        Assert.Equal(404, (await c.GetStreamingUrl(3)).Status()); // a mesterlista még nincs a tárolóban
        Assert.Equal(404, (await c.GetStreamingUrl(4)).Status()); // nincs ilyen film
    }

    [Fact]
    public async Task Stream_Hls_PointsToMasterProxyOnPublicUrl()
    {
        Owns(1);
        Movie(1, video: "1_master.m3u8");
        _blob.VideoExistsAsync("1_master.m3u8").Returns(true);

        var result = await Create().GetStreamingUrl(1);

        Assert.Equal("https://gw.test/api/Movie/1/hls-master", result.Field("url"));
        Assert.Equal(true, result.Field("isHls"));
    }

    [Fact]
    public async Task Stream_LegacyMp4_PrefersAvailable720p_ElseOriginal()
    {
        Owns(1); Owns(2); Owns(3);
        Movie(1, video: "1.mp4");
        Movie(2, video: "2.mp4");
        Movie(3, video: "3.mp4");
        _blob.VideoExistsAsync(Arg.Is<string>(f => f == "1_480p.mp4" || f == "1_720p.mp4" || f == "2.mp4")).Returns(true);
        _blob.GenerateSasUrlAsync(Arg.Any<string>(), 1).Returns(ci => $"https://blob/{ci.Arg<string>()}?sig=x");
        var c = Create();

        var withQualities = await c.GetStreamingUrl(1);
        Assert.Equal("720p", withQualities.Field("primaryQuality"));
        Assert.Equal("https://blob/1_720p.mp4?sig=x", withQualities.Field("url"));

        var original = await c.GetStreamingUrl(2);
        Assert.Equal("original", original.Field("primaryQuality"));

        Assert.Equal(404, (await c.GetStreamingUrl(3)).Status());
    }

    [Fact]
    public async Task Stream_StorageError_Is500()
    {
        Owns(1);
        Movie(1, video: "1_master.m3u8");
        _blob.VideoExistsAsync(Arg.Any<string>()).Returns<bool>(_ => throw new InvalidOperationException("tároló hiba"));

        Assert.Equal(500, (await Create().GetStreamingUrl(1)).Status());
    }

    [Fact]
    public async Task HlsMaster_ListsOnlyExistingQualities_ViaProxy()
    {
        Owns(1);
        Movie(1, video: "1_master.m3u8");
        _blob.VideoExistsAsync("1_480p.m3u8").Returns(true);
        _blob.VideoExistsAsync("1_1080p.m3u8").Returns(true);

        var result = (ContentResult)await Create(publicBaseUrl: null).GetHlsMasterPlaylist(1);

        Assert.Equal("application/vnd.apple.mpegurl", result.ContentType);
        Assert.Contains("BANDWIDTH=1000000,RESOLUTION=853x480", result.Content);
        Assert.Contains("/api/Movie/1/hls-quality/1080p", result.Content);
        Assert.DoesNotContain("720p", result.Content);
    }

    [Fact]
    public async Task HlsMaster_ForbiddenOrNotHls()
    {
        Owns(2);
        Movie(2, video: "2.mp4");
        var c = Create();

        Assert.Equal(403, (await c.GetHlsMasterPlaylist(1)).Status());
        Assert.Equal(404, (await c.GetHlsMasterPlaylist(2)).Status());
    }

    [Fact]
    public async Task HlsQuality_RewritesSegmentsToSignedUrls()
    {
        Owns(1);
        Movie(1, video: "1_master.m3u8");
        _blob.VideoExistsAsync("1_720p.m3u8").Returns(true);
        _blob.GenerateSasUrlAsync(Arg.Any<string>(), 2).Returns(ci => $"https://blob/{ci.Arg<string>()}?sig=s");
        _http.CreateClient(Arg.Any<string>()).Returns(new HttpClient(FakeHttpHandler.Json(
            "#EXTM3U\n#EXTINF:6.0,\n1_720p_000.ts\n#EXTINF:6.0,\n1_720p_001.ts\n#EXT-X-ENDLIST")));

        var result = (ContentResult)await Create().GetHlsQualityPlaylist(1, "720p");

        Assert.Contains("https://blob/1_720p_000.ts?sig=s", result.Content);
        Assert.Contains("https://blob/1_720p_001.ts?sig=s", result.Content);
        Assert.Contains("#EXT-X-ENDLIST", result.Content);
    }

    [Fact]
    public async Task HlsQuality_ErrorCases()
    {
        Owns(1); Owns(2); Owns(3);
        Movie(1, video: "1_master.m3u8");
        Movie(2, video: null);
        Movie(3, video: "3_master.m3u8");
        _blob.VideoExistsAsync("3_720p.m3u8").Returns(true);
        _blob.GenerateSasUrlAsync(Arg.Any<string>(), 2).Returns("https://blob/x");
        _http.CreateClient(Arg.Any<string>()).Returns(new HttpClient(FakeHttpHandler.Json("", HttpStatusCode.Forbidden)));
        var c = Create();

        Assert.Equal(403, (await c.GetHlsQualityPlaylist(9, "720p")).Status());
        Assert.Equal(404, (await c.GetHlsQualityPlaylist(2, "720p")).Status());
        Assert.Equal(404, (await c.GetHlsQualityPlaylist(1, "720p")).Status());
        Assert.Equal(500, (await c.GetHlsQualityPlaylist(3, "720p")).Status());
    }

    // ── Lejátszási pozíció ────────────────────────────────────────────────────

    [Fact]
    public async Task Progress_SaveCreatesThenUpdates_AndGetDefaultsToZero()
    {
        var c = Create();

        var empty = (VideoProgressDto)((OkObjectResult)(await c.GetProgress(5)).Result!).Value!;
        Assert.Equal(0, empty.ProgressSeconds);

        Assert.IsType<OkObjectResult>(await c.SaveProgress(5, new SaveProgressDto { ProgressSeconds = 42 }));
        Assert.IsType<OkObjectResult>(await Create().SaveProgress(5, new SaveProgressDto { ProgressSeconds = 90.5 }));
        Assert.IsType<BadRequestObjectResult>(await c.SaveProgress(5, new SaveProgressDto { ProgressSeconds = -1 }));

        var saved = (VideoProgressDto)((OkObjectResult)(await Create().GetProgress(5)).Result!).Value!;
        Assert.Equal(90.5, saved.ProgressSeconds);
        await using var db = _db.Create();
        Assert.Equal(1, await db.VideoProgresses.CountAsync());
    }
}
