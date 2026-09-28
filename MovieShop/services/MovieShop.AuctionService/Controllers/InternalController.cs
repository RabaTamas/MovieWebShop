using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.AuctionService.Data;

namespace MovieShop.AuctionService.Controllers;

/// <summary>
/// Service-ek közötti végpontok. A gateway nem teszi közzé őket kifelé.
/// </summary>
[ApiController]
[Route("api/internal/auctions")]
public class InternalController : ControllerBase
{
    private readonly AuctionsDbContext _db;

    public InternalController(AuctionsDbContext db) => _db = db;

    /// <summary>
    /// Van-e a felhasználónak licitje. A User Service a törlés előtt kérdezi meg:
    /// a monolitban a Bid → User kapcsolat Restrict volt, így licitáló nem volt törölhető.
    /// </summary>
    [HttpGet("users/{userId}/has-bids")]
    public async Task<IActionResult> HasBids(int userId)
        => Ok(new { hasBids = await _db.Bids.AnyAsync(b => b.UserId == userId) });
}
