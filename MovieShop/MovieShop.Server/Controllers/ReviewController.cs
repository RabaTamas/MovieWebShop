using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.Constants;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;
using System.Security.Claims;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly IAuthService _authService;

        public ReviewController(IReviewService reviewService, IAuthService authService)
        {
            _reviewService = reviewService;
            _authService = authService;
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReviewDto>>> GetAllReviews()
        {
            var reviews = await _reviewService.GetAllReviewsAsync();
            return Ok(reviews);
        }

        [HttpGet("movie/{movieId}")]
        public async Task<ActionResult<IEnumerable<ReviewDto>>> GetReviewsByMovie(int movieId)
        {
            var reviews = await _reviewService.GetReviewsByMovieIdAsync(movieId);
            return Ok(reviews);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ReviewDto>> GetReview(int id)
        {
            var review = await _reviewService.GetReviewByIdAsync(id);
            if (review == null)
                return NotFound();

            return Ok(review);
        }

        [Authorize]
        [HttpPost("movie/{movieId}")]
        public async Task<ActionResult> AddReview(int movieId, [FromBody] ReviewCreateDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (userId == 0)
                return Unauthorized();

            var result = await _reviewService.AddReviewAsync(movieId, userId, dto.Content);
            if (!result)
                return BadRequest(new { message = "Failed to add review" });

            return Ok(new { message = "Review added successfully" });
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateReview(int id, [FromBody] ReviewCreateDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (userId == 0)
                return Unauthorized();

            // Check if user owns the review
            if (!await _reviewService.UserOwnsReviewAsync(id, userId))
                return Forbid();

            var result = await _reviewService.UpdateReviewAsync(id, dto.Content, userId);
            if (!result)
                return NotFound();

            return Ok(new { message = "Review updated successfully" });
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteReview(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            if (userId == 0)
                return Unauthorized();

            var isAdmin = await _authService.IsAdminAsync(userId);
            var userOwnsReview = await _reviewService.UserOwnsReviewAsync(id, userId);

            // Check if user is allowed to delete the review
            if (!userOwnsReview && !isAdmin)
                return Forbid();

            var result = await _reviewService.DeleteReviewAsync(id, userId);
            if (!result)
                return NotFound();

            return Ok(new { message = "Review deleted successfully" });
        }
    }
}