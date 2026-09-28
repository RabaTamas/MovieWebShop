using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.OrderService.DTOs;
using MovieShop.OrderService.Services;

namespace MovieShop.OrderService.Controllers;

/// <summary>
/// Adminisztrátori kosárkezelés: bármely felhasználó kosarának megtekintése és
/// módosítása. A kosár az Order Service-hez tartozik, ezért ez a controller is ide került.
/// </summary>
[ApiController]
[Route("api/admin/shopping-carts")]
[Authorize(Roles = "Admin")]
public class AdminShoppingCartController : ControllerBase
{
    private readonly IShoppingCartService _cartService;

    public AdminShoppingCartController(IShoppingCartService cartService)
        => _cartService = cartService;

    [HttpGet("{userId}")]
    public async Task<ActionResult<ShoppingCartDto>> GetCartByUserId(int userId)
        => Ok(await _cartService.GetCartByUserIdAsync(userId));

    [HttpPost("{userId}/add")]
    public async Task<ActionResult> AddToCart(int userId, [FromBody] AddToCartDto dto)
        => await _cartService.AddToCartAsync(userId, dto.MovieId, dto.Quantity)
            ? Ok()
            : BadRequest("Failed to add to cart");

    [HttpPut("{userId}/update")]
    public async Task<ActionResult> UpdateQuantity(int userId, [FromBody] AddToCartDto dto)
        => await _cartService.UpdateCartItemQuantityAsync(userId, dto.MovieId, dto.Quantity)
            ? Ok()
            : BadRequest("Failed to update quantity");

    [HttpDelete("{userId}/remove/{movieId}")]
    public async Task<ActionResult> RemoveFromCart(int userId, int movieId)
        => await _cartService.RemoveFromCartAsync(userId, movieId)
            ? Ok()
            : NotFound("Item not found in cart");

    [HttpDelete("{userId}/clear")]
    public async Task<ActionResult> ClearCart(int userId)
        => await _cartService.ClearCartAsync(userId)
            ? Ok()
            : BadRequest("Failed to clear cart");
}
