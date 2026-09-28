using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MovieShop.UserService.Data;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Models;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;

namespace MovieShop.UserService.Services;

public interface IPushNotificationService
{
    bool IsConfigured { get; }
    string? PublicKey { get; }

    Task SubscribeAsync(int userId, PushSubscriptionDto dto, string? userAgent);
    Task<bool> UnsubscribeAsync(int userId, string endpoint);

    Task<PushSendResultDto> SendToUserAsync(int userId, PushMessageDto message);
    Task<PushSendResultDto> SendToAllAsync(PushMessageDto message);
}

/// <summary>
/// Web Push értesítések (VAPID) küldése — a monolit PushNotificationService-ének megfelelője.
/// Az „új film" értesítést itt nem a filmfelvétel indítja közvetlenül (a filmek a Catalog
/// Service-ben élnek), hanem a MovieChangedConsumer, amikor IsNew jelzésű esemény érkezik.
/// </summary>
public class PushNotificationService : IPushNotificationService
{
    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);
    private const int TimeToLiveSeconds = 60 * 60 * 24;

    private readonly UsersDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PushNotificationService> _logger;
    private readonly VapidAuthentication? _vapid;
    private readonly string? _publicKey;

    public PushNotificationService(
        UsersDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<PushNotificationService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var publicKey = configuration["WebPush:PublicKey"];
        var privateKey = configuration["WebPush:PrivateKey"];
        var subject = configuration["WebPush:Subject"] ?? "mailto:admin@movieshop.com";

        if (!string.IsNullOrWhiteSpace(publicKey) && !string.IsNullOrWhiteSpace(privateKey))
        {
            _vapid = new VapidAuthentication(publicKey, privateKey) { Subject = subject };
            _publicKey = publicKey;
        }
    }

    public bool IsConfigured => _vapid != null;
    public string? PublicKey => _publicKey;

    public async Task SubscribeAsync(int userId, PushSubscriptionDto dto, string? userAgent)
    {
        var existing = await _context.WebPushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);

        if (existing == null)
        {
            _context.WebPushSubscriptions.Add(new WebPushSubscription
            {
                Endpoint = dto.Endpoint,
                P256dh = dto.Keys.P256dh,
                Auth = dto.Keys.Auth,
                UserAgent = Truncate(userAgent, 300),
                UserId = userId
            });
        }
        else
        {
            existing.UserId = userId;
            existing.P256dh = dto.Keys.P256dh;
            existing.Auth = dto.Keys.Auth;
            existing.UserAgent = Truncate(userAgent, 300);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> UnsubscribeAsync(int userId, string endpoint)
    {
        var subscription = await _context.WebPushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == endpoint && s.UserId == userId);

        if (subscription == null)
            return false;

        _context.WebPushSubscriptions.Remove(subscription);
        await _context.SaveChangesAsync();
        return true;
    }

    public Task<PushSendResultDto> SendToUserAsync(int userId, PushMessageDto message) =>
        SendAsync(_context.WebPushSubscriptions.Where(s => s.UserId == userId), message);

    public Task<PushSendResultDto> SendToAllAsync(PushMessageDto message) =>
        SendAsync(_context.WebPushSubscriptions, message);

    /// <summary>Az „új film érkezett" értesítés tartalma — a monolittal azonos.</summary>
    public static PushMessageDto NewMovieMessage(int movieId, string title, string? description, string? imageUrl, int price, int? discountedPrice)
    {
        var priceText = discountedPrice.HasValue
            ? $"{discountedPrice.Value:N0} Ft (was {price:N0} Ft)"
            : $"{price:N0} Ft";

        return new PushMessageDto
        {
            Title = $"New movie: {title}",
            Body = string.IsNullOrWhiteSpace(description)
                ? $"Now available for {priceText}."
                : $"{Truncate(description, 110)} — {priceText}",
            Icon = "/pwa-192x192.png",
            Image = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl,
            Url = $"/movies/{movieId}",
            Tag = $"movie-{movieId}"
        };
    }

    private async Task<PushSendResultDto> SendAsync(IQueryable<WebPushSubscription> query, PushMessageDto message)
    {
        var result = new PushSendResultDto();

        if (_vapid == null)
        {
            _logger.LogWarning("Web Push is not configured (WebPush:PublicKey / WebPush:PrivateKey) — notification skipped");
            return result;
        }

        var subscriptions = await query.ToListAsync();
        if (subscriptions.Count == 0)
            return result;

        var payload = JsonSerializer.Serialize(message, PayloadJson);
        // RFC 8291 (aes128gcm) titkosítás és RFC 8292 VAPID fejléc ("Authorization: vapid t=…, k=…") —
        // az Apple push-szolgáltatása (iOS/Safari) csak ezt fogadja el, a Chrome és a Firefox is ezt ajánlja
        var client = new PushServiceClient(_httpClientFactory.CreateClient("WebPush"))
        {
            DefaultAuthentication = _vapid,
            DefaultAuthenticationScheme = VapidAuthenticationScheme.Vapid
        };
        var expired = new List<WebPushSubscription>();

        foreach (var subscription in subscriptions)
        {
            try
            {
                var pushSubscription = new PushSubscription { Endpoint = subscription.Endpoint };
                pushSubscription.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dh);
                pushSubscription.SetKey(PushEncryptionKeyName.Auth, subscription.Auth);

                await client.RequestPushMessageDeliveryAsync(
                    pushSubscription,
                    new PushMessage(payload) { TimeToLive = TimeToLiveSeconds });
                result.Sent++;
            }
            catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                expired.Add(subscription);
            }
            catch (Exception ex)
            {
                result.Failed++;
                _logger.LogWarning(ex, "Web Push delivery failed for subscription {SubscriptionId}", subscription.Id);
            }
        }

        if (expired.Count > 0)
        {
            _context.WebPushSubscriptions.RemoveRange(expired);
            await _context.SaveChangesAsync();
            result.Removed = expired.Count;
        }

        return result;
    }

    private static string? Truncate(string? value, int maxLength) =>
        value == null || value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
