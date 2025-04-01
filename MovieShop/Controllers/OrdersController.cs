using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.Data;
using MovieShop.Models;

namespace MovieShop.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : BaseController<Order>
    {
        public OrdersController(AppDbContext context) : base(context) { }

        // GET: api/orders/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByUser(int userId)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderMovies)
                .ThenInclude(om => om.Movie)
                .Where(o => o.UserId == userId)
                .ToListAsync();

            if (!orders.Any())
            {
                return NotFound("A felhasználónak nincs rendelése.");
            }

            return Ok(orders);
        }

        // POST: api/orders/create
        [HttpPost("create")]
        public async Task<ActionResult> CreateOrder(int userId)
        {
            var cart = await _context.ShoppingCarts
                .Include(c => c.ShoppingCartMovies)
                .ThenInclude(scm => scm.Movie)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.ShoppingCartMovies.Any())
            {
                return BadRequest("A kosár üres.");
            }

            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                TotalPrice = cart.ShoppingCartMovies.Sum(scm => scm.Quantity * scm.Movie.Price),
                OrderMovies = cart.ShoppingCartMovies.Select(scm => new OrderMovie
                {
                    MovieId = scm.MovieId,
                    Quantity = scm.Quantity,
                    PriceAtOrder = scm.Movie.Price
                }).ToList()
            };

            _context.Orders.Add(order);
            _context.ShoppingCarts.Remove(cart);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
        }
    }
}
