using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.ServiceDefaults;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Services;

namespace MovieShop.UserService.Controllers;

/// <summary>
/// Web Push feliratkozás kezelése — a monolit PushControllerével azonos útvonalak és válaszok,
/// a gatewayen át: /api/Push/*.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PushController : ControllerBase
{
    private readonly IPushNotificationService _pushService;

    public PushController(IPushNotificationService pushService) => _pushService = pushService;

    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey()
    {
        if (!_pushService.IsConfigured)
            return StatusCode(503, new { message = "Push notifications are not configured on the server." });

        return Ok(new { publicKey = _pushService.PublicKey });
    }

    [Authorize]
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionDto dto)
    {
        if (!_pushService.IsConfigured)
            return StatusCode(503, new { message = "Push notifications are not configured on the server." });

        await _pushService.SubscribeAsync(User.GetUserId(), dto, Request.Headers.UserAgent.ToString());
        return Ok(new { subscribed = true });
    }

    [Authorize]
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeDto dto)
    {
        var removed = await _pushService.UnsubscribeAsync(User.GetUserId(), dto.Endpoint);
        return Ok(new { subscribed = false, removed });
    }

    [Authorize]
    [HttpPost("test")]
    public async Task<ActionResult<PushSendResultDto>> SendTest()
    {
        if (!_pushService.IsConfigured)
            return StatusCode(503, new { message = "Push notifications are not configured on the server." });

        var result = await _pushService.SendToUserAsync(User.GetUserId(), new PushMessageDto
        {
            Title = "MovieWebShop notifications are on",
            Body = "You'll get a notification here when a new movie arrives.",
            Icon = "/pwa-192x192.png",
            Url = "/",
            Tag = "movieshop-test"
        });

        return Ok(result);
    }
}
