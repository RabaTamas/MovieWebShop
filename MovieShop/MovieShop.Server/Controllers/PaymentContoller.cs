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
                var clientSecret = await _stripeService.CreatePaymentIntent(request.Amount, "huf");

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
                var isSuccessful = await _stripeService.ConfirmPaymentIntent(request.PaymentIntentId);

                return Ok(new { success = isSuccessful });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            return Ok(new
            {
                publishableKey = _configuration["Stripe:PublishableKey"]
            });
        }
    }

    public class CreatePaymentIntentRequest
    {
        public decimal Amount { get; set; }
    }

    public class VerifyPaymentRequest
    {
        public string PaymentIntentId { get; set; } = string.Empty;
    }
}