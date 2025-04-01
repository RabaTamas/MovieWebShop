using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.Data;
using MovieShop.Models;

namespace MovieShop.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShoppingCartController : BaseController<ShoppingCart>
    {
        public ShoppingCartController(AppDbContext context) : base(context) { }

        // GET: api/shoppingcart/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<ShoppingCart>> GetCartByUser(int userId)
        {
            var cart = await _context.ShoppingCarts
                .Include(c => c.ShoppingCartMovies)
                .ThenInclude(scm => scm.Movie)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                return NotFound("A felhasználóhoz nem tartozik kosár.");
            }

            return Ok(cart);
        }

        // POST: api/shoppingcart/add
        [HttpPost("add")]
        public async Task<ActionResult> AddToCart(int userId, int movieId, int quantity)
        {
            var cart = await _context.ShoppingCarts
                .Include(c => c.ShoppingCartMovies)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new ShoppingCart { UserId = userId, ShoppingCartMovies = new List<ShoppingCartMovie>() };
                _context.ShoppingCarts.Add(cart);
            }

            var cartMovie = cart.ShoppingCartMovies.FirstOrDefault(scm => scm.MovieId == movieId);
            if (cartMovie != null)
            {
                cartMovie.Quantity += quantity;
            }
            else
            {
                cart.ShoppingCartMovies.Add(new ShoppingCartMovie { MovieId = movieId, ShoppingCartId = cart.Id, Quantity = quantity });
            }

            await _context.SaveChangesAsync();
            return Ok("Film hozzáadva a kosárhoz.");
        }

        // PUT: api/shoppingcart/update
        [HttpPut("update")]
        public async Task<IActionResult> UpdateCartItem(int userId, int movieId, int quantity)
        {
            var cart = await _context.ShoppingCarts
                .Include(c => c.ShoppingCartMovies)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null) return NotFound("Nincs ilyen kosár.");

            var cartMovie = cart.ShoppingCartMovies.FirstOrDefault(scm => scm.MovieId == movieId);
            if (cartMovie == null) return NotFound("A film nincs a kosárban.");

            if (quantity <= 0)
            {
                cart.ShoppingCartMovies.Remove(cartMovie);
            }
            else
            {
                cartMovie.Quantity = quantity;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
