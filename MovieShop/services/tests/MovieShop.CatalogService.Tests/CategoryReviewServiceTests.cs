using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Services;

namespace MovieShop.CatalogService.Tests;

public class CategoryServiceTests
{
    private readonly CatalogDb _db = new();
    private readonly IMovieService _movies = Substitute.For<IMovieService>();

    private CategoryService CreateService() =>
        new(_db.Create(), Mapping.Create(), _movies, NullLogger<CategoryService>.Instance);

    [Fact]
    public async Task GetAll_IsOrderedByName_AndGetByIdFindsOne()
    {
        _db.AddCategory("Thriller");
        var action = _db.AddCategory("Action");
        var service = CreateService();

        Assert.Equal(["Action", "Thriller"], (await service.GetAllCategoriesAsync()).Select(c => c.Name));
        Assert.Equal("Action", (await service.GetCategoryByIdAsync(action.Id))!.Name);
        Assert.Null(await service.GetCategoryByIdAsync(999));
        Assert.True(await service.CategoryExistsAsync(action.Id));
        Assert.False(await service.CategoryExistsAsync(999));
    }

    [Fact]
    public async Task Add_TrimsName_AndRejectsCaseInsensitiveDuplicate()
    {
        var dto = new CategoryDto { Name = "  Sci-Fi " };

        Assert.True(await CreateService().AddCategoryAsync(dto));
        Assert.NotEqual(0, dto.Id);
        Assert.False(await CreateService().AddCategoryAsync(new CategoryDto { Name = "sci-fi " }));

        await using var db = _db.Create();
        Assert.Equal("Sci-Fi", (await db.Categories.SingleAsync()).Name);
    }

    [Fact]
    public async Task Update_RenamesAndRepublishesAffectedMovies()
    {
        var movie = _db.AddMovie("Film", categories: "Akcio");
        await using var db0 = _db.Create();
        var id = (await db0.Categories.SingleAsync()).Id;

        Assert.True(await CreateService().UpdateCategoryAsync(id, new CategoryDto { Id = id, Name = "Action" }));

        await _movies.Received(1).PublishMovieChangedAsync(movie.Id);
        await using var db = _db.Create();
        Assert.Equal("Action", (await db.Categories.SingleAsync()).Name);
    }

    [Fact]
    public async Task Update_UnknownOrDuplicateName_ReturnsFalse()
    {
        var action = _db.AddCategory("Action");
        var drama = _db.AddCategory("Drama");
        var service = CreateService();

        Assert.False(await service.UpdateCategoryAsync(999, new CategoryDto { Name = "X" }));
        Assert.False(await service.UpdateCategoryAsync(drama.Id, new CategoryDto { Name = "ACTION" }));
        Assert.True(await service.UpdateCategoryAsync(action.Id, new CategoryDto { Name = "Action" }));
    }

    [Fact]
    public async Task Delete_KeepsMovies_RemovesLink_AndRepublishes()
    {
        var movie = _db.AddMovie("Film", categories: "Action");
        await using var db0 = _db.Create();
        var id = (await db0.Categories.SingleAsync()).Id;

        Assert.Equal(1, await CreateService().GetMovieCountByCategoryAsync(id));
        Assert.True(await CreateService().DeleteCategoryAsync(id));
        Assert.False(await CreateService().DeleteCategoryAsync(id));

        await _movies.Received(1).PublishMovieChangedAsync(movie.Id);
        await using var db = _db.Create();
        Assert.Empty(db.Categories);
        Assert.Empty((await db.Movies.Include(m => m.Categories).SingleAsync()).Categories);
    }

    [Fact]
    public async Task WriteOperations_WhenDatabaseFails_ReturnFalse()
    {
        var broken = _db.Create();
        await broken.DisposeAsync();
        var service = new CategoryService(broken, Mapping.Create(), _movies, NullLogger<CategoryService>.Instance);

        Assert.False(await service.AddCategoryAsync(new CategoryDto { Name = "X" }));
        Assert.False(await service.UpdateCategoryAsync(1, new CategoryDto { Name = "X" }));
        Assert.False(await service.DeleteCategoryAsync(1));
    }
}

public class ReviewServiceTests
{
    private readonly CatalogDb _db = new();

    private ReviewService CreateService() => new(_db.Create(), NullLogger<ReviewService>.Instance);

    [Fact]
    public async Task Queries_ProjectMovieAndAuthor()
    {
        var movie = _db.AddMovie("Film");
        _db.AddUser(7, "neo");
        _db.AddReview(movie.Id, 7, "Jó");
        _db.AddReview(movie.Id, 8, "Szerző nélkül");
        var service = CreateService();

        var all = (await service.GetAllReviewsAsync()).ToList();
        Assert.Equal(2, all.Count);
        Assert.All(all, r => Assert.Equal("Film", r.Movie!.Title));
        Assert.Contains(all, r => r.UserName == "neo" && r.User!.Name == "neo");
        Assert.Contains(all, r => r.UserName == "Unknown user" && r.User!.Email == "");

        Assert.Equal(2, (await service.GetReviewsByMovieIdAsync(movie.Id)).Count());
        Assert.Empty(await service.GetReviewsByMovieIdAsync(999));
        Assert.Equal("Jó", (await service.GetReviewByIdAsync(all.First(r => r.UserId == 7).Id))!.Content);
        Assert.Null(await service.GetReviewByIdAsync(999));
    }

    [Fact]
    public async Task Add_RequiresExistingMovie()
    {
        var movie = _db.AddMovie("Film");

        Assert.True(await CreateService().AddReviewAsync(movie.Id, 7, "Jó"));
        Assert.False(await CreateService().AddReviewAsync(999, 7, "Nincs film"));
    }

    [Fact]
    public async Task Update_OnlyByOwner_AndDelete()
    {
        var movie = _db.AddMovie("Film");
        _db.AddReview(movie.Id, 7, "Eredeti");
        await using var db0 = _db.Create();
        var id = (await db0.Reviews.SingleAsync()).Id;
        var service = CreateService();

        Assert.True(await service.UserOwnsReviewAsync(id, 7));
        Assert.False(await service.UserOwnsReviewAsync(id, 8));
        Assert.False(await service.UserOwnsReviewAsync(999, 7));

        Assert.False(await CreateService().UpdateReviewAsync(id, "Más írta", 8));
        Assert.False(await CreateService().UpdateReviewAsync(999, "x", 7));
        Assert.True(await CreateService().UpdateReviewAsync(id, "Javított", 7));

        await using (var db = _db.Create())
            Assert.Equal("Javított", (await db.Reviews.SingleAsync()).Content);

        Assert.True(await CreateService().DeleteReviewAsync(id));
        Assert.False(await CreateService().DeleteReviewAsync(id));
    }

    [Fact]
    public async Task WriteOperations_WhenDatabaseFails_ReturnFalse()
    {
        var broken = _db.Create();
        await broken.DisposeAsync();
        var service = new ReviewService(broken, NullLogger<ReviewService>.Instance);

        Assert.False(await service.AddReviewAsync(1, 1, "x"));
        Assert.False(await service.UpdateReviewAsync(1, "x", 1));
        Assert.False(await service.DeleteReviewAsync(1));
    }
}

public class EntitlementServiceTests
{
    [Fact]
    public async Task HasAccess_AndEntitledIds_ReadTheProjection()
    {
        var db = new CatalogDb();
        db.Grant(7, 1);
        db.Grant(7, 2);
        db.Grant(8, 1);
        var service = new EntitlementService(db.Create());

        Assert.True(await service.HasAccessAsync(7, 2));
        Assert.False(await service.HasAccessAsync(8, 2));
        Assert.Equal([1, 2], (await service.GetEntitledMovieIdsAsync(7)).Order());
        Assert.Empty(await service.GetEntitledMovieIdsAsync(9));
    }
}
