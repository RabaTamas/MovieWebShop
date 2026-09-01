using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IRecommendationService
    {
        Task<RecommendationsResultDto> GetRecommendationsAsync(int userId, int count = 5);
        Task<string> GetRecommendationContextAsync(int userId);
    }
}
