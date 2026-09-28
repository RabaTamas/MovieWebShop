using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Controllers
{
    /// <summary>
    /// Web Push feliratkozás kezelése. A frontend (usePushNotifications hook) innen kéri a
    /// nyilvános VAPID kulcsot, ide küldi a böngésző feliratkozását, és próbaértesítést is kérhet.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PushController : ControllerBase
    {
        private readonly IPushNotificationService _pushService;

        public PushController(IPushNotificationService pushService)
        {
            _pushService = pushService;
        }

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

            await _pushService.SubscribeAsync(GetCurrentUserId(), dto, Request.Headers.UserAgent.ToString());
            return Ok(new { subscribed = true });
        }

        [Authorize]
        [HttpPost("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeDto dto)
        {
            var removed = await _pushService.UnsubscribeAsync(GetCurrentUserId(), dto.Endpoint);
            return Ok(new { subscribed = false, removed });
        }

        /// <summary>Próbaértesítés a bejelentkezett felhasználó összes feliratkozott eszközére.</summary>
        [Authorize]
        [HttpPost("test")]
        public async Task<ActionResult<PushSendResultDto>> SendTest()
        {
            if (!_pushService.IsConfigured)
                return StatusCode(503, new { message = "Push notifications are not configured on the server." });

            var result = await _pushService.SendToUserAsync(GetCurrentUserId(), new PushMessageDto
            {
                Title = "MovieWebShop notifications are on",
                Body = "You'll get a notification here when a new movie arrives.",
                Icon = "/pwa-192x192.png",
                Url = "/",
                Tag = "movieshop-test"
            });

            return Ok(result);
        }

        private int GetCurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
    }
}
