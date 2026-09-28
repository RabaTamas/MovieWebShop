using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.AuctionService.DTOs;
using MovieShop.AuctionService.Services;
using MovieShop.ServiceDefaults;

namespace MovieShop.AuctionService.Controllers;

/// <summary>
/// A monolit AuctionController-ével azonos végpontok, státuszkódok és válaszkulcsok
/// (a kliens `data.error` és `paid` mezőket olvas).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuctionController : ControllerBase
{
    private readonly IAuctionService _auctionService;

    public AuctionController(IAuctionService auctionService) => _auctionService = auctionService;

    // GET /api/Auction — aktív és induló aukciók (nyilvános)
    [HttpGet]
    public async Task<IActionResult> GetAuctions()
        => Ok(await _auctionService.GetActiveAuctionsAsync());

    // GET /api/Auction/{id} — egy aukció licitelőzménnyel (nyilvános)
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAuction(int id)
    {
        var auction = await _auctionService.GetAuctionAsync(id);
        return auction == null ? NotFound() : Ok(auction);
    }

    // GET /api/Auction/my-wins — a bejelentkezett felhasználó nyert aukciói
    [HttpGet("my-wins")]
    [Authorize]
    public async Task<IActionResult> GetMyWonAuctions()
    {
        var userId = User.GetUserId();
        if (userId == 0) return Unauthorized();

        return Ok(await _auctionService.GetMyWonAuctionsAsync(userId));
    }

    // GET /api/Auction/all — minden aukció (admin)
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllAuctions()
        => Ok(await _auctionService.GetAllAuctionsAsync());

    // POST /api/Auction — aukció létrehozása (admin)
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAuction([FromBody] CreateAuctionRequest request)
    {
        var auction = await _auctionService.CreateAuctionAsync(request);
        return CreatedAtAction(nameof(GetAuction), new { id = auction.Id }, auction);
    }

    // PUT /api/Auction/{id} — aukció szerkesztése (admin)
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditAuction(int id, [FromBody] CreateAuctionRequest request)
    {
        var result = await _auctionService.EditAuctionAsync(id, request);
        return result == null ? NotFound() : Ok(result);
    }

    // DELETE /api/Auction/{id} — aukció törlése (admin)
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAuction(int id)
    {
        var ok = await _auctionService.DeleteAuctionAsync(id);
        return ok ? NoContent() : NotFound();
    }

    // POST /api/Auction/{id}/create-payment — Stripe PaymentIntent a nyertesnek
    [HttpPost("{id}/create-payment")]
    [Authorize]
    public async Task<IActionResult> CreatePayment(int id)
    {
        var userId = User.GetUserId();
        if (userId == 0) return Unauthorized();

        var result = await _auctionService.CreatePaymentIntentAsync(id, userId);
        if (result == null)
            return BadRequest(new { error = "Payment not available for this auction." });

        return Ok(new { clientSecret = result.Value.clientSecret, publishableKey = result.Value.publishableKey });
    }

    // POST /api/Auction/{id}/confirm-payment — fizetés ellenőrzése és kifizetettnek jelölés
    [HttpPost("{id}/confirm-payment")]
    [Authorize]
    public async Task<IActionResult> ConfirmPayment(int id, [FromBody] ConfirmAuctionPaymentRequest request)
    {
        var userId = User.GetUserId();
        if (userId == 0) return Unauthorized();

        var ok = await _auctionService.ConfirmPaymentAsync(id, userId, request.PaymentIntentId);
        return ok ? Ok(new { paid = true }) : BadRequest(new { error = "Payment verification failed." });
    }

    // POST /api/Auction/{id}/bid — licit leadása
    [HttpPost("{id}/bid")]
    [Authorize]
    public async Task<IActionResult> PlaceBid(int id, [FromBody] PlaceBidRequest request)
    {
        var userId = User.GetUserId();
        if (userId == 0) return Unauthorized();

        var result = await _auctionService.PlaceBidAsync(id, userId, request.Amount);
        return result.Success ? Ok(result) : BadRequest(new { error = result.Error });
    }
}
