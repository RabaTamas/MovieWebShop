using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.Data;
using MovieShop.Models;

namespace MovieShop.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : BaseController<Review>
    {
        public ReviewsController(AppDbContext context) : base(context) { }

        // GET: api/reviews/movie/5 - Egy adott film összes véleménye
        [HttpGet("movie/{movieId}")]
        public async Task<ActionResult<IEnumerable<Review>>> GetReviewsByMovie(int movieId)
        {
            var reviews = await _context.Reviews
                                        .Where(r => r.MovieId == movieId)
                                        .Include(r => r.User)
                                        .ToListAsync();

            if (!reviews.Any())
            {
                return NotFound($"Nincsenek vélemények ezzel a film ID-val: {movieId}");
            }

            return Ok(reviews);
        }

        // GET: api/reviews/user/2 - Egy adott felhasználó véleményei
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<Review>>> GetReviewsByUser(int userId)
        {
            var reviews = await _context.Reviews
                                        .Where(r => r.UserId == userId)
                                        .Include(r => r.Movie)
                                        .ToListAsync();

            if (!reviews.Any())
            {
                return NotFound($"A felhasználónak ({userId}) nincs véleménye.");
            }

            return Ok(reviews);
        }
    }
}
