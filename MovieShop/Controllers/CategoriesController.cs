using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.Data;
using MovieShop.Models;

namespace MovieShop.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : BaseController<Category>
    {
        public CategoriesController(AppDbContext context) : base(context) { }

        // GET: api/categories/2/movies
        [HttpGet("{categoryId}/movies")]
        public async Task<ActionResult<IEnumerable<Movie>>> GetMoviesByCategory(int categoryId)
        {
            var category = await _context.Categories
                                         .Include(c => c.Movies)
                                         .FirstOrDefaultAsync(c => c.Id == categoryId);

            if (category == null)
            {
                return NotFound($"Nincs ilyen kategória ezzel az ID-val: {categoryId}");
            }

            return Ok(category.Movies);
        }
    }
}
