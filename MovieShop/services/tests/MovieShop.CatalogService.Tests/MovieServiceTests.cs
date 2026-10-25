using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Services;
using MovieShop.Contracts.Events;

namespace MovieShop.CatalogService.Tests;

public class MovieServiceTests
{
    private readonly CatalogDb _db = new();
    private readonly ITmdbService _tmdb = Substitute.For<ITmdbService>();
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();

    private MovieService CreateService() =>
        new(_db.Create(), Mapping.Create(), _tmdb, _publish, NullLogger<MovieService>.Instance);

    [Fact]
    public async Task Listings_SeparateActiveAndDeletedMovies()
    {
        _db.AddMovie("Aktív");
        _db.AddMovie("Törölt", deleted: true);
        var service = CreateService();

        Assert.Equal(["Aktív"], (await service.GetAllMoviesAsync()).Select(m => m.Title));
        Assert.Equal(2, (await service.GetAllMoviesForAdminAsync()).Count());
        var deleted = Assert.Single(await service.GetDeletedMoviesAsync());
        Assert.Equal("Deleted", deleted.Status);
    }

    [Fact]
    public async Task GetMovieById_IncludesReviewsWithAuthor_AndTmdbInfo()
    {
        var movie = _db.AddMovie("The Matrix", categories: "Action");
        _db.AddUser(7, "neo");
        _db.AddReview(movie.Id, 7, "Kiváló");
        _db.AddReview(movie.Id, 99, "Ismeretlen szerzőtől");
        _tmdb.SearchMovieAsync("The Matrix").Returns(new TmdbMovieDto { Id = 603, VoteAverage = 8.2, VoteCount = 100, ReleaseDate = "1999-03-30" });

        var dto = await CreateService().GetMovieByIdAsync(movie.Id);

        Assert.NotNull(dto);
        Assert.Equal(603, dto.TmdbInfo!.TmdbId);
        Assert.Equal("1999-03-30", dto.TmdbInfo.ReleaseDate);
        Assert.Contains(dto.Reviews!, r => r.UserName == "neo" && r.User!.Email == "neo@test.hu");
        Assert.Contains(dto.Reviews!, r => r.UserName == "Unknown user");
        Assert.Equal("Action", Assert.Single(dto.Categories!).Name);
    }

    [Fact]
    public async Task GetMovieById_WithoutTmdbMatch_HasNoTmdbInfo_AndDeletedIsHidden()
    {
        var movie = _db.AddMovie("Saját film");
        var deleted = _db.AddMovie("Törölt", deleted: true);

        var service = CreateService();

        Assert.Null((await service.GetMovieByIdAsync(movie.Id))!.TmdbInfo);
        Assert.Null(await service.GetMovieByIdAsync(deleted.Id));
        Assert.Null(await service.GetMovieByIdAsync(999));
    }

    [Fact]
    public async Task GetMovieForAdmin_FindsDeletedMovies_Too()
    {
        var deleted = _db.AddMovie("Törölt", deleted: true);
        var service = CreateService();

        Assert.Equal("Törölt", (await service.GetMovieByIdForAdminAsync(deleted.Id))!.Title);
        Assert.Null(await service.GetMovieByIdForAdminAsync(999));
    }

    [Fact]
    public async Task CategoryFilters_ReturnMatchingActiveMovies()
    {
        var action = _db.AddCategory("Action");
        var drama = _db.AddCategory("Drama");
        _db.AddMovie("A", categories: "Action");
        _db.AddMovie("D", categories: "Drama");
        _db.AddMovie("Törölt A", deleted: true, categories: "Action");
        var service = CreateService();

        Assert.Equal(["A"], (await service.GetMoviesByCategoryAsync(action.Id)).Select(m => m.Title));
        Assert.Equal(2, (await service.GetMoviesByCategoriesAsync([action.Id, drama.Id])).Count());
        Assert.Empty(await service.GetMoviesByCategoriesAsync([]));
        Assert.Empty(await service.GetMoviesByCategoriesAsync(null!));
    }

    [Fact]
    public async Task AddMovie_SavesWithCategories_AndPublishesNewMovieEvent()
    {
        var action = _db.AddCategory("Action");
        var dto = new MovieDetailsDto
        {
            Title = "Dune", Description = "Arrakis", Price = 1500, DiscountedPrice = 1200, ImageUrl = "dune.jpg",
            Categories = [new CategoryDto { Id = action.Id }, new CategoryDto { Id = 999 }]
        };

        var ok = await CreateService().AddMovieAsync(dto);

        Assert.True(ok);
        Assert.NotEqual(0, dto.Id);
        await using var db = _db.Create();
        var saved = await db.Movies.Include(m => m.Categories).SingleAsync();
        Assert.Equal("Action", Assert.Single(saved.Categories).Name);
        await _publish.Received(1).Publish(
            Arg.Is<MovieChanged>(e => e.MovieId == dto.Id && e.IsNew && e.Categories.Contains("Action") && e.DiscountedPrice == 1200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddMovie_WhenPublishFails_ReturnsFalse()
    {
        _publish.Publish(Arg.Any<MovieChanged>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("bróker nem elérhető")));

        Assert.False(await CreateService().AddMovieAsync(new MovieDetailsDto { Title = "X" }));
    }

    [Fact]
    public async Task UpdateMovie_ReplacesFieldsAndCategories_AndPublishes()
    {
        var movie = _db.AddMovie("Régi cím", categories: "Action");
        var drama = _db.AddCategory("Drama");

        var ok = await CreateService().UpdateMovieAsync(movie.Id, new MovieDetailsDto
        {
            Title = "Új cím", Description = "új", Price = 2000, ImageUrl = "uj.jpg",
            Categories = [new CategoryDto { Id = drama.Id }]
        });

        Assert.True(ok);
        await using var db = _db.Create();
        var saved = await db.Movies.Include(m => m.Categories).SingleAsync();
        Assert.Equal("Új cím", saved.Title);
        Assert.Equal("Drama", Assert.Single(saved.Categories).Name);
        await _publish.Received(1).Publish(Arg.Is<MovieChanged>(e => e.Title == "Új cím" && !e.IsNew), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateMovie_WithoutCategoryList_KeepsCategories_AndUnknownIdIsFalse()
    {
        var movie = _db.AddMovie("Film", categories: "Action");
        var service = CreateService();

        Assert.True(await service.UpdateMovieAsync(movie.Id, new MovieDetailsDto { Title = "Film 2", Categories = null }));
        Assert.False(await service.UpdateMovieAsync(999, new MovieDetailsDto { Title = "x" }));

        await using var db = _db.Create();
        Assert.Single((await db.Movies.Include(m => m.Categories).SingleAsync()).Categories);
    }

    [Fact]
    public async Task DeleteAndRestore_ToggleSoftDelete_AndPublish()
    {
        var movie = _db.AddMovie("Film");
        var service = CreateService();

        Assert.True(await service.DeleteMovieAsync(movie.Id));
        Assert.False(await service.RestoreMovieAsync(999));
        await using (var db = _db.Create())
        {
            var saved = await db.Movies.SingleAsync();
            Assert.True(saved.IsDeleted);
            Assert.NotNull(saved.DeletedAt);
        }

        Assert.True(await CreateService().RestoreMovieAsync(movie.Id));
        await using (var db = _db.Create())
        {
            var saved = await db.Movies.SingleAsync();
            Assert.False(saved.IsDeleted);
            Assert.Null(saved.DeletedAt);
        }

        Assert.False(await CreateService().DeleteMovieAsync(999));
        await _publish.Received(1).Publish(Arg.Is<MovieChanged>(e => e.IsDeleted), Arg.Any<CancellationToken>());
        await _publish.Received(1).Publish(Arg.Is<MovieChanged>(e => !e.IsDeleted), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAndRemoveCategory_AreIdempotent_AndPublishOnlyOnChange()
    {
        var movie = _db.AddMovie("Film");
        var action = _db.AddCategory("Action");

        Assert.True(await CreateService().AddCategoryToMovieAsync(movie.Id, action.Id));
        Assert.True(await CreateService().AddCategoryToMovieAsync(movie.Id, action.Id));
        Assert.False(await CreateService().AddCategoryToMovieAsync(movie.Id, 999));
        Assert.False(await CreateService().AddCategoryToMovieAsync(999, action.Id));

        Assert.True(await CreateService().RemoveCategoryFromMovieAsync(movie.Id, action.Id));
        Assert.True(await CreateService().RemoveCategoryFromMovieAsync(movie.Id, action.Id));
        Assert.False(await CreateService().RemoveCategoryFromMovieAsync(999, action.Id));

        await _publish.Received(2).Publish(Arg.Any<MovieChanged>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateVideoFileName_SavesAndPublishes_UnknownMovieIsIgnored()
    {
        var movie = _db.AddMovie("Film");

        await CreateService().UpdateVideoFileNameAsync(movie.Id, "1_master.m3u8");
        await CreateService().UpdateVideoFileNameAsync(999, "x");
        await CreateService().PublishMovieChangedAsync(999);

        await using var db = _db.Create();
        Assert.Equal("1_master.m3u8", (await db.Movies.SingleAsync()).VideoFileName);
        await _publish.Received(1).Publish(Arg.Is<MovieChanged>(e => e.VideoFileName == "1_master.m3u8"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteOperations_WhenDatabaseFails_ReturnFalse()
    {
        var movie = _db.AddMovie("Film");
        var category = _db.AddCategory("Action");
        var broken = _db.Create();
        await broken.DisposeAsync();
        var service = new MovieService(broken, Mapping.Create(), _tmdb, _publish, NullLogger<MovieService>.Instance);

        Assert.False(await service.UpdateMovieAsync(movie.Id, new MovieDetailsDto()));
        Assert.False(await service.DeleteMovieAsync(movie.Id));
        Assert.False(await service.RestoreMovieAsync(movie.Id));
        Assert.False(await service.AddCategoryToMovieAsync(movie.Id, category.Id));
        Assert.False(await service.RemoveCategoryFromMovieAsync(movie.Id, category.Id));
    }
}
