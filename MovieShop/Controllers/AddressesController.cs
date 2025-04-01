using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.Data;
using MovieShop.Models;

namespace MovieShop.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AddressesController : BaseController<Address>
    {
        public AddressesController(AppDbContext context) : base(context) { }

        // GET: api/addresses/user/2 - Egy adott felhasználó címei
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<Address>>> GetAddressesByUser(int userId)
        {
            var addresses = await _context.Addresses
                                          .Where(a => a.UserId == userId)
                                          .ToListAsync();

            if (!addresses.Any())
            {
                return NotFound($"A felhasználónak ({userId}) nincs mentett címe.");
            }

            return Ok(addresses);
        }
    }
}
