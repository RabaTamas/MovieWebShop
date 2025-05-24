using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Constants;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/admin/shopping-carts")]
    [Authorize(Roles = UserRoles.Admin)]
    public class AdminShoppingCartController : ControllerBase
    {
        private readonly IShoppingCartService _cartService;

        public AdminShoppingCartController(IShoppingCartService cartService)
        {
            _cartService = cartService;
        }

        // Get a specific user's cart by their ID
        [HttpGet("{userId}")]
        public async Task<ActionResult<ShoppingCartDto>> GetCartByUserId(int userId)
        {
            var cart = await _cartService.GetCartByUserIdAsync(userId);
            if (cart == null)
            {
                return NotFound();
            }
            return Ok(cart);
        }

        // Add item to a user's cart
        [HttpPost("{userId}/add")]
        public async Task<ActionResult> AddToCart(int userId, [FromBody] AddToCartDto dto)
        {
            var result = await _cartService.AddToCartAsync(userId, dto.MovieId, dto.Quantity);
            return result ? Ok() : BadRequest("Failed to add to cart");
        }

        // Update quantity of an item in a user's cart
        [HttpPut("{userId}/update")]
        public async Task<ActionResult> UpdateQuantity(int userId, [FromBody] AddToCartDto dto)
        {
            var result = await _cartService.UpdateCartItemQuantityAsync(userId, dto.MovieId, dto.Quantity);
            return result ? Ok() : BadRequest("Failed to update quantity");
        }

        // Remove an item from a user's cart
        [HttpDelete("{userId}/remove/{movieId}")]
        public async Task<ActionResult> RemoveFromCart(int userId, int movieId)
        {
            var result = await _cartService.RemoveFromCartAsync(userId, movieId);
            return result ? Ok() : NotFound("Item not found in cart");
        }

        // Clear a user's cart
        [HttpDelete("{userId}/clear")]
        public async Task<ActionResult> ClearCart(int userId)
        {
            var result = await _cartService.ClearCartAsync(userId);
            return result ? Ok() : BadRequest("Failed to clear cart");
        }
    }
}
