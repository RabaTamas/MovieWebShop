using System.Text;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.CatalogService.Controllers;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Services;

namespace MovieShop.CatalogService.Tests;

public class CategoryControllerTests
{
    private readonly ICategoryService _service = Substitute.For<ICategoryService>();

    private CategoryController Create() => new(_service, NullLogger<CategoryController>.Instance);

    [Fact]
    public async Task Reads_ValidateIds()
    {
        _service.GetAllCategoriesAsync().Returns([new CategoryDto { Id = 1, Name = "Action" }]);
        _service.GetCategoryByIdAsync(1).Returns(new CategoryDto { Id = 1 });
        _service.CategoryExistsAsync(1).Returns(true);
        _service.GetMovieCountByCategoryAsync(1).Returns(4);
        var c = Create();

        Assert.IsType<OkObjectResult>((await c.GetAllCategories()).Result);
        Assert.IsType<OkObjectResult>((await c.GetCategory(1)).Result);
        Assert.IsType<NotFoundObjectResult>((await c.GetCategory(2)).Result);
        Assert.IsType<BadRequestObjectResult>((await c.GetCategory(0)).Result);
        Assert.IsType<OkResult>(await c.CategoryExists(1));
        Assert.IsType<NotFoundResult>(await c.CategoryExists(2));
        Assert.IsType<BadRequestResult>(await c.CategoryExists(-1));
        Assert.Equal(4, (await c.GetMovieCountByCategory(1)).Field("movieCount"));
        Assert.IsType<BadRequestObjectResult>((await c.GetMovieCountByCategory(0)).Result);
    }

    [Fact]
    public async Task Add_ValidatesNameAndDuplicates()
    {
        _service.AddCategoryAsync(Arg.Is<CategoryDto>(d => d.Name == "Action")).Returns(true);
        var c = Create();

        Assert.IsType<CreatedAtActionResult>(await c.AddCategory(new CategoryDto { Name = "Action" }));
        Assert.IsType<BadRequestObjectResult>(await c.AddCategory(new CategoryDto { Name = "Drama" }));
        Assert.IsType<BadRequestObjectResult>(await c.AddCategory(null!));
        Assert.IsType<BadRequestObjectResult>(await c.AddCategory(new CategoryDto { Name = " " }));
        Assert.IsType<BadRequestObjectResult>(await c.AddCategory(new CategoryDto { Name = new string('x', 101) }));
    }

    [Fact]
    public async Task Update_ValidatesEverything()
    {
        _service.UpdateCategoryAsync(1, Arg.Any<CategoryDto>()).Returns(true);
        var c = Create();

        Assert.IsType<NoContentResult>(await c.UpdateCategory(1, new CategoryDto { Id = 1, Name = "A" }));
        Assert.IsType<NotFoundObjectResult>(await c.UpdateCategory(2, new CategoryDto { Id = 2, Name = "A" }));
        Assert.IsType<BadRequestObjectResult>(await c.UpdateCategory(0, new CategoryDto()));
        Assert.IsType<BadRequestObjectResult>(await c.UpdateCategory(1, null!));
        Assert.IsType<BadRequestObjectResult>(await c.UpdateCategory(1, new CategoryDto { Id = 2, Name = "A" }));
        Assert.IsType<BadRequestObjectResult>(await c.UpdateCategory(1, new CategoryDto { Id = 1, Name = "" }));
        Assert.IsType<BadRequestObjectResult>(await c.UpdateCategory(1, new CategoryDto { Id = 1, Name = new string('x', 101) }));
    }

    [Fact]
    public async Task Delete_ValidatesAndReportsMissing()
    {
        _service.DeleteCategoryAsync(1).Returns(true);
        var c = Create();

        Assert.IsType<NoContentResult>(await c.DeleteCategory(1));
        Assert.IsType<NotFoundObjectResult>(await c.DeleteCategory(2));
        Assert.IsType<BadRequestObjectResult>(await c.DeleteCategory(0));
    }

    [Fact]
    public async Task ServiceErrors_Become500()
    {
        _service.GetAllCategoriesAsync().Returns<IEnumerable<CategoryDto>>(_ => throw new Exception());
        _service.GetCategoryByIdAsync(Arg.Any<int>()).Returns<CategoryDto?>(_ => throw new Exception());
        _service.AddCategoryAsync(Arg.Any<CategoryDto>()).Returns<bool>(_ => throw new Exception());
        _service.UpdateCategoryAsync(Arg.Any<int>(), Arg.Any<CategoryDto>()).Returns<bool>(_ => throw new Exception());
        _service.GetMovieCountByCategoryAsync(Arg.Any<int>()).Returns<int>(_ => throw new Exception());
        _service.CategoryExistsAsync(Arg.Any<int>()).Returns<bool>(_ => throw new Exception());
        var c = Create();

        Assert.Equal(500, ((ObjectResult)(await c.GetAllCategories()).Result!).StatusCode);
        Assert.Equal(500, ((ObjectResult)(await c.GetCategory(1)).Result!).StatusCode);
        Assert.Equal(500, (await c.AddCategory(new CategoryDto { Name = "A" })).Status());
        Assert.Equal(500, (await c.UpdateCategory(1, new CategoryDto { Id = 1, Name = "A" })).Status());
        Assert.Equal(500, (await c.DeleteCategory(1)).Status());
        Assert.Equal(500, (await c.CategoryExists(1)).Status());
        Assert.Equal(500, ((ObjectResult)(await c.GetMovieCountByCategory(1)).Result!).StatusCode);
    }
}

public class ReviewControllerTests
{
    private readonly IReviewService _service = Substitute.For<IReviewService>();

    private ReviewController Create() => new(_service);

    [Fact]
    public async Task Reads()
    {
        _service.GetAllReviewsAsync().Returns([]);
        _service.GetReviewsByMovieIdAsync(1).Returns([]);
        _service.GetReviewByIdAsync(1).Returns(new ReviewDto { Id = 1 });
        var c = Create();

        Assert.IsType<OkObjectResult>((await c.GetAllReviews()).Result);
        Assert.IsType<OkObjectResult>((await c.GetReviewsByMovie(1)).Result);
        Assert.IsType<OkObjectResult>((await c.GetReview(1)).Result);
        Assert.IsType<NotFoundResult>((await c.GetReview(2)).Result);
    }

    [Fact]
    public async Task Add_RequiresUser_AndReportsFailure()
    {
        _service.AddReviewAsync(1, 7, "Jó").Returns(true);

        Assert.IsType<OkObjectResult>(await Create().As(7).AddReview(1, new ReviewCreateDto { Content = "Jó" }));
        Assert.IsType<BadRequestObjectResult>(await Create().As(7).AddReview(2, new ReviewCreateDto { Content = "Jó" }));
        Assert.IsType<UnauthorizedResult>(await Create().Anonymous().AddReview(1, new ReviewCreateDto()));
    }

    [Fact]
    public async Task Update_OnlyOwner()
    {
        _service.UserOwnsReviewAsync(1, 7).Returns(true);
        _service.UserOwnsReviewAsync(2, 7).Returns(true);
        _service.UpdateReviewAsync(1, "új", 7).Returns(true);

        Assert.IsType<OkObjectResult>(await Create().As(7).UpdateReview(1, new ReviewCreateDto { Content = "új" }));
        Assert.IsType<NotFoundResult>(await Create().As(7).UpdateReview(2, new ReviewCreateDto { Content = "új" }));
        Assert.IsType<ForbidResult>(await Create().As(8).UpdateReview(1, new ReviewCreateDto { Content = "új" }));
        Assert.IsType<UnauthorizedResult>(await Create().Anonymous().UpdateReview(1, new ReviewCreateDto()));
    }

    [Fact]
    public async Task Delete_OwnerOrAdmin()
    {
        _service.UserOwnsReviewAsync(1, 7).Returns(true);
        _service.DeleteReviewAsync(1).Returns(true);

        Assert.IsType<OkObjectResult>(await Create().As(7).DeleteReview(1));
        Assert.IsType<OkObjectResult>(await Create().As(99, role: "Admin").DeleteReview(1));
        Assert.IsType<ForbidResult>(await Create().As(8).DeleteReview(1));
        Assert.IsType<NotFoundResult>(await Create().As(99, role: "Admin").DeleteReview(2));
        Assert.IsType<UnauthorizedResult>(await Create().Anonymous().DeleteReview(1));
    }
}

public class SearchAndInternalControllerTests
{
    [Fact]
    public async Task Search_DelegatesToSearchProvider()
    {
        var es = Substitute.For<IElasticsearchService>();
        es.SearchAsync(Arg.Any<SearchRequestDto>()).Returns(new SearchResultDto { Total = 3 });
        es.AutocompleteAsync("mat").Returns(["The Matrix"]);
        var c = new SearchController(es);

        Assert.Equal(3, ((SearchResultDto)((OkObjectResult)(await c.Search(new SearchRequestDto())).Result!).Value!).Total);
        Assert.IsType<OkObjectResult>((await c.Autocomplete("mat")).Result);
        Assert.IsType<OkObjectResult>(await c.Reindex());
        await es.Received(1).ReindexAllAsync();
    }

    [Fact]
    public async Task Internal_RepublishesMovies_AndReturnsMoviesWithReviews()
    {
        var db = new CatalogDb();
        var matrix = db.AddMovie("The Matrix", video: "1_master.m3u8", categories: "Action");
        var other = db.AddMovie("Inception");
        db.AddMovie("Törölt", deleted: true);
        db.AddUser(7, "neo");
        db.AddReview(matrix.Id, 7, "Jó");
        db.AddReview(matrix.Id, 8, "Névtelen");
        var movies = Substitute.For<IMovieService>();
        var entitlements = Substitute.For<IEntitlementService>();
        entitlements.GetEntitledMovieIdsAsync(7).Returns([matrix.Id]);
        var c = new InternalController(db.Create(), movies, entitlements, NullLogger<InternalController>.Instance);

        Assert.Equal(3, (await c.RepublishAll()).Field("republished"));
        await movies.Received(3).PublishMovieChangedAsync(Arg.Any<int>());

        var all = ((IEnumerable<object>)((OkObjectResult)await c.GetMovies()).Value!).ToList();
        Assert.Equal(2, all.Count);
        var first = all[0];
        Assert.Equal(true, first.GetType().GetProperty("hasVideo")!.GetValue(first));
        var reviews = (List<object>)first.GetType().GetProperty("reviews")!.GetValue(first)!;
        Assert.Equal(2, reviews.Count);
        Assert.Contains(reviews, r => (string)r.GetType().GetProperty("userName")!.GetValue(r)! == "Anonymous");

        var filtered = ((IEnumerable<object>)((OkObjectResult)await c.GetMovies([other.Id])).Value!).ToList();
        Assert.Single(filtered);

        Assert.Equal([matrix.Id], (List<int>)((OkObjectResult)await c.GetEntitlements(7)).Value!);
    }
}

public class AdminVideoControllerTests
{
    private readonly IMovieService _movies = Substitute.For<IMovieService>();
    private readonly IBlobStorageService _blob = Substitute.For<IBlobStorageService>();

    public AdminVideoControllerTests()
    {
        GlobalConfiguration.Configuration.UseInMemoryStorage();
        _movies.GetMovieByIdAsync(1).Returns(new MovieDetailsWithTmdbDto { Id = 1, VideoFileName = "1_master.m3u8" });
        _movies.GetMovieByIdAsync(2).Returns(new MovieDetailsWithTmdbDto { Id = 2, VideoFileName = null });
        _movies.GetMovieByIdAsync(3).Returns(new MovieDetailsWithTmdbDto { Id = 3, VideoFileName = "3.mp4" });
    }

    private AdminVideoController Create() => new(_movies, _blob, NullLogger<AdminVideoController>.Instance);

    private static IFormFile File(string name, int size = 16)
        => new FormFile(new MemoryStream(new byte[size]), 0, size, "videoFile", name);

    [Fact]
    public async Task Upload_StoresOriginal_SetsFileName_AndQueuesTranscoding()
    {
        var result = await Create().UploadVideo(1, File("film.MP4"));

        Assert.Equal("1.mp4", result.Field("originalFileName"));
        Assert.Equal("transcoding", result.Field("status"));
        Assert.NotNull(result.Field("jobId"));
        await _blob.Received(1).UploadVideoAsync(Arg.Any<Stream>(), "1.mp4");
        await _movies.Received(1).UpdateVideoFileNameAsync(1, "1.mp4");
    }

    [Fact]
    public async Task Upload_Validation_AndStorageFailure()
    {
        _blob.UploadVideoAsync(Arg.Any<Stream>(), Arg.Any<string>()).Returns<string>(_ => throw new Exception("tároló"));
        var c = Create();

        Assert.IsType<BadRequestObjectResult>(await c.UploadVideo(1, null!));
        Assert.IsType<BadRequestObjectResult>(await c.UploadVideo(1, File("film.mp4", size: 0)));
        Assert.IsType<BadRequestObjectResult>(await c.UploadVideo(1, File("film.avi")));
        Assert.IsType<NotFoundObjectResult>(await c.UploadVideo(9, File("film.mp4")));
        Assert.Equal(500, (await c.UploadVideo(1, File("film.mp4"))).Status());
    }

    [Fact]
    public async Task Delete_RemovesAllMovieFiles()
    {
        _blob.ListFilesAsync("1").Returns(["1.mp4", "1_master.m3u8", "1_480p_000.ts"]);
        var c = Create();

        Assert.IsType<OkObjectResult>(await c.DeleteVideo(1));
        await _blob.Received(3).DeleteVideoAsync(Arg.Any<string>());
        await _movies.Received(1).UpdateVideoFileNameAsync(1, null);

        Assert.IsType<NotFoundObjectResult>(await c.DeleteVideo(9));
        Assert.IsType<BadRequestObjectResult>(await c.DeleteVideo(2));
    }

    [Fact]
    public async Task Delete_StorageFailure_Is500()
    {
        _blob.ListFilesAsync(Arg.Any<string>()).Returns<List<string>>(_ => throw new Exception());

        Assert.Equal(500, (await Create().DeleteVideo(1)).Status());
    }

    [Fact]
    public async Task Info_ReportsTranscodingState()
    {
        var existing = new[] { "1_master.m3u8", "1.mp4", "1_720p.m3u8", "3.mp4", "3_480p.mp4" };
        _blob.VideoExistsAsync(Arg.Is<string>(f => existing.Contains(f))).Returns(true);
        _blob.GetVideoSizeAsync("1.mp4").Returns(2L * 1024 * 1024);
        var c = Create();

        var hls = await c.GetVideoInfo(1);
        Assert.Equal(true, hls.Field("transcodingComplete"));
        Assert.Equal(2.0, hls.Field("fileSizeMB"));
        var versions = (Dictionary<string, bool>)hls.Field("transcodedVersions")!;
        Assert.True(versions["720p"]);
        Assert.False(versions["1080p"]);

        var legacy = await c.GetVideoInfo(3);
        Assert.Equal(false, legacy.Field("transcodingComplete"));
        Assert.Equal(false, legacy.Field("isHls"));

        Assert.Equal(false, (await c.GetVideoInfo(2)).Field("hasVideo"));
        Assert.IsType<NotFoundObjectResult>(await c.GetVideoInfo(9));
    }

    [Fact]
    public async Task ListFiles_ReturnsBlobNames()
    {
        _blob.ListFilesAsync("1").Returns(["1.mp4"]);

        Assert.Equal(new List<string> { "1.mp4" }, (await Create().ListMovieFiles(1)).Field("files"));
    }
}
