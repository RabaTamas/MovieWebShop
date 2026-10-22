using MovieShop.OrderService.Models;
using MovieShop.OrderService.Services;

namespace MovieShop.OrderService.Tests;

public class RecommendationServiceTests : IDisposable
{
    private const int UserId = 1;
    private readonly OrdersDb _db = new();

    private RecommendationService CreateService() => new(_db.CreateContext());

    [Fact]
    public async Task NoPurchases_ReturnsEmptyRecommendations()
    {
        _db.SeedMovie(1, "The Matrix", 999, categories: "Action");

        var result = await CreateService().GetRecommendationsAsync(UserId);

        Assert.Empty(result.CategoryBased);
        Assert.Empty(result.CollaborativeBased);
    }

    [Fact]
    public async Task CategoryBased_RanksMatchingMovies_ByPopularity_AndSkipsOwnedAndDeleted()
    {
        _db.SeedMovie(1, "The Matrix", 999, categories: "Action;Sci-Fi");
        _db.SeedMovie(2, "John Wick", 999, categories: "Action");
        _db.SeedMovie(3, "Die Hard", 999, categories: "Action");
        _db.SeedMovie(4, "Notebook", 999, categories: "Romance");
        _db.SeedMovie(5, "Deleted Action", 999, categories: "Action", isDeleted: true);

        _db.SeedOrder(UserId, OrderStatus.Completed, 1);
        // A Die Hard népszerűbb, ezért előrébb kell kerülnie
        _db.SeedOrder(50, OrderStatus.Completed, 3);
        _db.SeedOrder(51, OrderStatus.Completed, 3);

        var result = await CreateService().GetRecommendationsAsync(UserId);

        Assert.Equal([3, 2], result.CategoryBased.Select(r => r.Id));
    }

    [Fact]
    public async Task Collaborative_RecommendsWhatSimilarUsersBought()
    {
        _db.SeedMovie(1, "The Matrix", 999, categories: "Sci-Fi");
        _db.SeedMovie(2, "Inception", 1299, categories: "Thriller");
        _db.SeedMovie(3, "Interstellar", 1299, categories: "Drama");

        _db.SeedOrder(UserId, OrderStatus.Completed, 1);
        // Két hasonló ízlésű felhasználó is megvette a Matrixot és az Inceptiont
        _db.SeedOrder(20, OrderStatus.Completed, 1, 2);
        _db.SeedOrder(21, OrderStatus.Completed, 1, 2, 3);
        // Egy nem hasonló felhasználó vásárlása nem számít
        _db.SeedOrder(22, OrderStatus.Completed, 3);

        var result = await CreateService().GetRecommendationsAsync(UserId);

        Assert.Equal(2, result.CollaborativeBased.First().Id);
        Assert.DoesNotContain(result.CollaborativeBased, r => r.Id == 1);
    }

    public void Dispose() => _db.Dispose();
}
