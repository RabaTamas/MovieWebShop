using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;
using System.Security.Claims;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuctionController : ControllerBase
    {
        private readonly IAuctionService _auctionService;

        public AuctionController(IAuctionService auctionService)
        {
            _auctionService = auctionService;
        }

        // GET /api/Auction — list active & pending auctions (public)
        [HttpGet]
        public async Task<IActionResult> GetAuctions()
        {
            var auctions = await _auctionService.GetActiveAuctionsAsync();
            return Ok(auctions);
        }

        // GET /api/Auction/{id} — get single auction with bid history (public)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAuction(int id)
        {
            var auction = await _auctionService.GetAuctionAsync(id);
            return auction == null ? NotFound() : Ok(auction);
        }

        // GET /api/Auction/my-wins — auctions won by the current user (authorized)
        [HttpGet("my-wins")]
        [Authorize]
        public async Task<IActionResult> GetMyWonAuctions()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);
            var auctions = await _auctionService.GetMyWonAuctionsAsync(userId);
            return Ok(auctions);
        }

        // GET /api/Auction/all — list ALL auctions (admin only, includes ended)
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllAuctions()
        {
            var auctions = await _auctionService.GetAllAuctionsAsync();
            return Ok(auctions);
        }

        // POST /api/Auction — create auction (admin only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateAuction([FromBody] CreateAuctionRequest request)
        {
            var auction = await _auctionService.CreateAuctionAsync(request);
            return CreatedAtAction(nameof(GetAuction), new { id = auction.Id }, auction);
        }

        // PUT /api/Auction/{id} — edit auction (admin, Pending only)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditAuction(int id, [FromBody] CreateAuctionRequest request)
        {
            var result = await _auctionService.EditAuctionAsync(id, request);
            return result == null ? NotFound() : Ok(result);
        }

        // DELETE /api/Auction/{id} — delete auction (admin only)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAuction(int id)
        {
            var ok = await _auctionService.DeleteAuctionAsync(id);
            return ok ? NoContent() : NotFound();
        }

        // POST /api/Auction/{id}/create-payment — create Stripe PaymentIntent for winner
        [HttpPost("{id}/create-payment")]
        [Authorize]
        public async Task<IActionResult> CreatePayment(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var result = await _auctionService.CreatePaymentIntentAsync(id, userId);
            if (result == null) return BadRequest(new { error = "Payment not available for this auction." });

            return Ok(new { clientSecret = result.Value.clientSecret, publishableKey = result.Value.publishableKey });
        }

        // POST /api/Auction/{id}/confirm-payment — verify Stripe payment and mark as paid
        [HttpPost("{id}/confirm-payment")]
        [Authorize]
        public async Task<IActionResult> ConfirmPayment(int id, [FromBody] ConfirmAuctionPaymentRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr == null) return Unauthorized();
            var userId = int.Parse(userIdStr);

            var ok = await _auctionService.ConfirmPaymentAsync(id, userId, request.PaymentIntentId);
            return ok ? Ok(new { paid = true }) : BadRequest(new { error = "Payment verification failed." });
        }

        // POST /api/Auction/{id}/bid — place a bid (authenticated users)
        [HttpPost("{id}/bid")]
        [Authorize]
        public async Task<IActionResult> PlaceBid(int id, [FromBody] PlaceBidRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr == null) return Unauthorized();

            var userId = int.Parse(userIdStr);
            var result = await _auctionService.PlaceBidAsync(id, userId, request.Amount);

            return result.Success ? Ok(result) : BadRequest(new { error = result.Error });
        }
    }
}
