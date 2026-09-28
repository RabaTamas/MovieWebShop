using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Services;
using MovieShop.ServiceDefaults;

namespace MovieShop.CatalogService.Controllers;

/// <summary>
/// A monolit ReviewController-ével azonos végpontok, státuszkódok és válaszok.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewController(IReviewService reviewService) => _reviewService = reviewService;

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetAllReviews()
        => Ok(await _reviewService.GetAllReviewsAsync());

    [HttpGet("movie/{movieId}")]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetReviewsByMovie(int movieId)
        => Ok(await _reviewService.GetReviewsByMovieIdAsync(movieId));

    [HttpGet("{id}")]
    public async Task<ActionResult<ReviewDto>> GetReview(int id)
    {
        var review = await _reviewService.GetReviewByIdAsync(id);
        return review == null ? NotFound() : Ok(review);
    }

    [Authorize]
    [HttpPost("movie/{movieId}")]
    public async Task<ActionResult> AddReview(int movieId, [FromBody] ReviewCreateDto dto)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        if (!await _reviewService.AddReviewAsync(movieId, userId, dto.Content))
            return BadRequest(new { message = "Failed to add review" });

        return Ok(new { message = "Review added successfully" });
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateReview(int id, [FromBody] ReviewCreateDto dto)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        if (!await _reviewService.UserOwnsReviewAsync(id, userId))
            return Forbid();

        if (!await _reviewService.UpdateReviewAsync(id, dto.Content, userId))
            return NotFound();

        return Ok(new { message = "Review updated successfully" });
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteReview(int id)
    {
        var userId = User.GetUserId();
        if (userId == 0)
            return Unauthorized();

        // A monolit a szerepkört adatbázisból kérdezte le; itt a tokenben lévő
        // szerepkör-claim ugyanezt az információt adja, hálózati hívás nélkül.
        var isAdmin = User.IsAdmin();
        var userOwnsReview = await _reviewService.UserOwnsReviewAsync(id, userId);

        if (!userOwnsReview && !isAdmin)
            return Forbid();

        if (!await _reviewService.DeleteReviewAsync(id))
            return NotFound();

        return Ok(new { message = "Review deleted successfully" });
    }
}
