﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;
using System.Security.Claims;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ShoppingCartController : ControllerBase
    {
        private readonly IShoppingCartService _cartService;

        public ShoppingCartController(IShoppingCartService cartService)
        {
            _cartService = cartService;
        }

        private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        [HttpGet]
        public async Task<ActionResult<ShoppingCartDto>> GetCart()
        {
            var userId = GetUserId();
            var cart = await _cartService.GetCartByUserIdAsync(userId);
            return Ok(cart);
        }

        [HttpPost("add")]
        public async Task<ActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            var userId = GetUserId();
            var result = await _cartService.AddToCartAsync(userId, dto.MovieId, dto.Quantity);
            return result ? Ok() : BadRequest("Failed to add to cart");
        }

        [HttpPut("update")]
        public async Task<ActionResult> UpdateQuantity([FromBody] AddToCartDto dto)
        {
            var userId = GetUserId();
            var result = await _cartService.UpdateCartItemQuantityAsync(userId, dto.MovieId, dto.Quantity);
            return result ? Ok() : BadRequest("Failed to update quantity");
        }

        [HttpDelete("remove/{movieId}")]
        public async Task<ActionResult> RemoveFromCart(int movieId)
        {
            var userId = GetUserId();
            var result = await _cartService.RemoveFromCartAsync(userId, movieId);
            return result ? Ok() : NotFound();
        }

        [HttpDelete("clear")]
        public async Task<ActionResult> ClearCart()
        {
            var userId = GetUserId();
            var result = await _cartService.ClearCartAsync(userId);
            return result ? Ok() : BadRequest("Failed to clear cart");
        }
    }
}
