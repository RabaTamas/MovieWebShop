using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;
using MovieShop.Server.Services.Interfaces.Stripe;
using System.Security.Claims;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        private readonly IStripeService _stripeService;

        public OrderController(IOrderService orderService, IStripeService stripeService)
        {
            _orderService = orderService;
            _stripeService = stripeService;
        }

        //[HttpPost]
        //public async Task<IActionResult> CreateOrder([FromBody] OrderRequestDto dto)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);
        //    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        //    var order = await _orderService.CreateOrderFromCartAsync(userId, dto);
        //    if (order == null)
        //        return BadRequest("Unable to create order. Cart may be empty or address invalid.");

        //    return Ok(order);
        //}


        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] OrderRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            
            if (!string.IsNullOrEmpty(dto.PaymentIntentId))
            {
                var paymentValid = await _stripeService.ConfirmPaymentIntent(dto.PaymentIntentId);
                if (!paymentValid)
                    return BadRequest("Payment verification failed.");
            }

            var order = await _orderService.CreateOrderFromCartAsync(userId, dto);
            if (order == null)
                return BadRequest("Unable to create order. Cart may be empty or address invalid.");

            return Ok(order);
        }

        [HttpGet("user")]
        public async Task<ActionResult<List<OrderDto>>> GetUserOrders()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var orders = await _orderService.GetOrdersByUserIdAsync(userId);
            return Ok(orders);
        }
    }


}
