using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.OrderService.DTOs;
using MovieShop.OrderService.Services;
using MovieShop.ServiceDefaults;

namespace MovieShop.OrderService.Controllers;

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

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] OrderRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!string.IsNullOrEmpty(dto.PaymentIntentId))
        {
            if (!await _stripeService.ConfirmPaymentIntent(dto.PaymentIntentId))
                return BadRequest("Payment verification failed.");
        }

        var order = await _orderService.CreateOrderFromCartAsync(User.GetUserId(), dto);

        return order == null
            ? BadRequest("Unable to create order. Cart may be empty or address invalid.")
            : Ok(order);
    }

    [HttpGet("user")]
    public async Task<ActionResult<List<OrderDto>>> GetUserOrders()
        => Ok(await _orderService.GetOrdersByUserIdAsync(User.GetUserId()));

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id, User.GetUserId());
        return order == null ? NotFound() : Ok(order);
    }
}

[ApiController]
[Route("api/admin/[controller]")]
[Authorize(Roles = "Admin")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService) => _orderService = orderService;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAllOrders()
        => Ok(await _orderService.GetAllOrdersAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var order = await _orderService.GetOrderByIdAdminAsync(id);
        return order == null ? NotFound() : Ok(order);
    }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrdersByStatus(string status)
        => Ok(await _orderService.GetOrdersByStatusAsync(status));

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        return await _orderService.UpdateOrderStatusAsync(id, dto.Status) ? NoContent() : NotFound();
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<OrderStatisticsDto>> GetOrderStatistics()
        => Ok(await _orderService.GetOrderStatisticsAsync());
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShoppingCartController : ControllerBase
{
    private readonly IShoppingCartService _cartService;
    private readonly IOrderService _orderService;

    public ShoppingCartController(IShoppingCartService cartService, IOrderService orderService)
    {
        _cartService = cartService;
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<ActionResult<ShoppingCartDto>> GetCart()
        => Ok(await _cartService.GetCartByUserIdAsync(User.GetUserId()));

    [HttpPost("add")]
    public async Task<ActionResult> AddToCart([FromBody] AddToCartDto dto)
    {
        var userId = User.GetUserId();

        // A „már megvetted" ellenőrzés lokális marad: a rendelések itt vannak.
        if (await _orderService.HasUserPurchasedMovieAsync(userId, dto.MovieId))
            return BadRequest(new { message = "You already own this movie. Check My Movies to watch it." });

        return await _cartService.AddToCartAsync(userId, dto.MovieId, dto.Quantity)
            ? Ok()
            : BadRequest("Failed to add to cart");
    }

    [HttpPut("update")]
    public async Task<ActionResult> UpdateQuantity([FromBody] AddToCartDto dto)
        => await _cartService.UpdateCartItemQuantityAsync(User.GetUserId(), dto.MovieId, dto.Quantity)
            ? Ok()
            : BadRequest("Failed to update quantity");

    [HttpDelete("remove/{movieId}")]
    public async Task<ActionResult> RemoveFromCart(int movieId)
        => await _cartService.RemoveFromCartAsync(User.GetUserId(), movieId) ? Ok() : NotFound();

    [HttpDelete("clear")]
    public async Task<ActionResult> ClearCart()
        => await _cartService.ClearCartAsync(User.GetUserId()) ? Ok() : BadRequest("Failed to clear cart");
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IStripeService _stripeService;
    private readonly IConfiguration _configuration;

    public PaymentController(IStripeService stripeService, IConfiguration configuration)
    {
        _stripeService = stripeService;
        _configuration = configuration;
    }

    [HttpPost("create-payment-intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
    {
        try
        {
            var clientSecret = await _stripeService.CreatePaymentIntent(request.Amount);

            return Ok(new
            {
                clientSecret,
                publishableKey = _configuration["Stripe:PublishableKey"]
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
    {
        try
        {
            return Ok(new { success = await _stripeService.ConfirmPaymentIntent(request.PaymentIntentId) });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
        => Ok(new { publishableKey = _configuration["Stripe:PublishableKey"] });
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;

    public RecommendationController(IRecommendationService recommendationService)
        => _recommendationService = recommendationService;

    [HttpGet]
    public async Task<ActionResult<RecommendationsResultDto>> GetRecommendations([FromQuery] int count = 5)
        => Ok(await _recommendationService.GetRecommendationsAsync(User.GetUserId(), count));
}
