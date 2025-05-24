using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IReviewService
    {
        Task<IEnumerable<ReviewDto>> GetAllReviewsAsync();
        Task<IEnumerable<ReviewDto>> GetReviewsByMovieIdAsync(int movieId);
        Task<ReviewDto?> GetReviewByIdAsync(int id);
        Task<bool> AddReviewAsync(int movieId, int userId, string content);
        Task<bool> UpdateReviewAsync(int id, string content, int userId);
        Task<bool> DeleteReviewAsync(int id, int userId);
        Task<bool> UserOwnsReviewAsync(int reviewId, int userId);
    }
}
