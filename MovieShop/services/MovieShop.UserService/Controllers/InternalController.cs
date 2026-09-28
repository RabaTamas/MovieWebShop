using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.Contracts.Events;
using MovieShop.UserService.Data;

namespace MovieShop.UserService.Controllers;

/// <summary>
/// Service-ek közötti, nem publikus végpontok. A gateway ezeket NEM teszi közzé
/// kifelé — csak a belső Docker-hálózatról érhetők el.
/// </summary>
[ApiController]
[Route("api/internal/users")]
public class InternalController : ControllerBase
{
    private readonly UsersDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<InternalController> _logger;

    public InternalController(
        UsersDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<InternalController> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Az összes felhasználó újrapublikálása UserChanged eseményként.
    ///
    /// Erre azért van szükség, mert az eseményvezérelt replikáció csak a jövőbeli
    /// változásokat közvetíti. Ha egy új service csatlakozik a rendszerhez (vagy a
    /// snapshot táblája elveszik), ezzel a végponttal tölthető fel a kiinduló állapot.
    /// </summary>
    [HttpPost("republish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RepublishAll()
    {
        var users = await _context.Users
            .AsNoTracking()
            .Select(u => new { u.Id, u.UserName, u.Email })
            .ToListAsync();

        foreach (var user in users)
        {
            await _publishEndpoint.Publish(new UserChanged
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                IsDeleted = false
            });
        }

        _logger.LogInformation("{Count} felhasználó újrapublikálva", users.Count);
        return Ok(new { republished = users.Count });
    }

    /// <summary>
    /// Felhasználónevek kötegelt lekérdezése azonosító alapján. A Catalog Service
    /// használja akkor, ha egy értékelés szerzője még nem szerepel a snapshotjában
    /// (pl. az esemény elveszett) — kiegészítő útvonal, nem a fő mechanizmus.
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] int[] userIds)
    {
        if (userIds.Length == 0)
            return Ok(Array.Empty<object>());

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .AsNoTracking()
            .Select(u => new { userId = u.Id, userName = u.UserName ?? "", email = u.Email ?? "" })
            .ToListAsync();

        return Ok(users);
    }
}
