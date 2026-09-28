using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Models;

namespace MovieShop.CatalogService.Services;

public interface IReviewService
{
    Task<IEnumerable<ReviewDto>> GetAllReviewsAsync();
    Task<IEnumerable<ReviewDto>> GetReviewsByMovieIdAsync(int movieId);
    Task<ReviewDto?> GetReviewByIdAsync(int id);
    Task<bool> AddReviewAsync(int movieId, int userId, string content);
    Task<bool> UpdateReviewAsync(int id, string content, int userId);
    Task<bool> DeleteReviewAsync(int id);
    Task<bool> UserOwnsReviewAsync(int reviewId, int userId);
}

public class ReviewService : IReviewService
{
    private readonly CatalogDbContext _context;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(CatalogDbContext context, ILogger<ReviewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<ReviewDto>> GetAllReviewsAsync()
        => await Project(_context.Reviews).ToListAsync();

    public async Task<IEnumerable<ReviewDto>> GetReviewsByMovieIdAsync(int movieId)
        => await Project(_context.Reviews
                .Where(r => r.MovieId == movieId)
                .OrderByDescending(r => r.CreatedAt))
            .ToListAsync();

    public async Task<ReviewDto?> GetReviewByIdAsync(int id)
        => await Project(_context.Reviews.Where(r => r.Id == id)).FirstOrDefaultAsync();

    /// <summary>
    /// A monolithoz hasonlóan csak a film létezését ellenőrzi. A felhasználó
    /// létezését a JWT token igazolja, amit a User Service írt alá.
    /// </summary>
    public async Task<bool> AddReviewAsync(int movieId, int userId, string content)
    {
        try
        {
            if (!await _context.Movies.AnyAsync(m => m.Id == movieId))
                return false;

            await _context.Reviews.AddAsync(new Review
            {
                Content = content,
                MovieId = movieId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding review for movie {MovieId}", movieId);
            return false;
        }
    }

    public async Task<bool> UpdateReviewAsync(int id, string content, int userId)
    {
        try
        {
            var review = await _context.Reviews.FindAsync(id);

            if (review == null || review.UserId != userId)
                return false;

            review.Content = content;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating review {ReviewId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteReviewAsync(int id)
    {
        try
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
                return false;

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review {ReviewId}", id);
            return false;
        }
    }

    public async Task<bool> UserOwnsReviewAsync(int reviewId, int userId)
    {
        var review = await _context.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reviewId);
        return review != null && review.UserId == userId;
    }

    /// <summary>
    /// A monolit AutoMapperes leképezésének megfelelő alak: beágyazott `movie` és
    /// `user` objektum. A film a saját táblából, a felhasználó a UserSnapshot
    /// projekcióból jön — mindez egyetlen SQL lekérdezés.
    /// </summary>
    private IQueryable<ReviewDto> Project(IQueryable<Review> source)
        => source
            .AsNoTracking()
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                Content = r.Content,
                UserId = r.UserId,
                MovieId = r.MovieId,
                UserName = _context.UserSnapshots
                    .Where(u => u.UserId == r.UserId)
                    .Select(u => u.UserName)
                    .FirstOrDefault() ?? "Unknown user",
                User = new UserDto
                {
                    Id = r.UserId,
                    Name = _context.UserSnapshots.Where(u => u.UserId == r.UserId).Select(u => u.UserName).FirstOrDefault() ?? "Unknown user",
                    Email = _context.UserSnapshots.Where(u => u.UserId == r.UserId).Select(u => u.Email).FirstOrDefault() ?? "",
                    Role = ""
                },
                Movie = new MovieListDto
                {
                    Id = r.Movie.Id,
                    Title = r.Movie.Title,
                    ImageUrl = r.Movie.ImageUrl,
                    Price = r.Movie.Price,
                    DiscountedPrice = r.Movie.DiscountedPrice,
                    IsDeleted = r.Movie.IsDeleted,
                    CreatedAt = r.Movie.CreatedAt,
                    DeletedAt = r.Movie.DeletedAt
                },
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            });
}
