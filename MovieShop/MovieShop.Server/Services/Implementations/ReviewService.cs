using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Services.Implementations
{
    public class ReviewService : IReviewService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        public ReviewService(AppDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ReviewDto>> GetAllReviewsAsync()
        {
            var reviews = await _context.Reviews
                .Include(r => r.User)
                .Include(r => r.Movie)
                .AsNoTracking()
                .ToListAsync();

            return _mapper.Map<IEnumerable<ReviewDto>>(reviews);
        }

        public async Task<IEnumerable<ReviewDto>> GetReviewsByMovieIdAsync(int movieId)
        {
            var reviews = await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.MovieId == movieId)
                .OrderByDescending(r => r.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return _mapper.Map<IEnumerable<ReviewDto>>(reviews);
        }

        public async Task<ReviewDto?> GetReviewByIdAsync(int id)
        {
            var review = await _context.Reviews
                .Include(r => r.User)
                .Include(r => r.Movie)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            return review == null ? null : _mapper.Map<ReviewDto>(review);
        }

        public async Task<bool> AddReviewAsync(int movieId, int userId, string content)
        {
            try
            {
                // Check if movie exists
                var movie = await _context.Movies.FindAsync(movieId);
                if (movie == null)
                    return false;

                // Check if user exists
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                // Create and save review
                var review = new Review
                {
                    Content = content,
                    MovieId = movieId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.Reviews.AddAsync(review);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateReviewAsync(int id, string content, int userId)
        {
            try
            {
                var review = await _context.Reviews.FindAsync(id);

                // Check if review exists and belongs to user
                if (review == null || review.UserId != userId)
                    return false;

                review.Content = content;
                review.UpdatedAt = DateTime.UtcNow;

                _context.Reviews.Update(review);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteReviewAsync(int id, int userId)
        {
            try
            {
                var review = await _context.Reviews.FindAsync(id);

                // Check if review exists
                if (review == null)
                    return false;

                // We don't need to check ownership here, as that's done in the controller
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UserOwnsReviewAsync(int reviewId, int userId)
        {
            var review = await _context.Reviews.FindAsync(reviewId);
            return review != null && review.UserId == userId;
        }
    }
}