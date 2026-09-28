using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IPushNotificationService
    {
        /// <summary>Van-e beállított VAPID kulcspár (WebPush:PublicKey / WebPush:PrivateKey).</summary>
        bool IsConfigured { get; }

        /// <summary>A nyilvános VAPID kulcs — a böngésző ezzel iratkozik fel (applicationServerKey).</summary>
        string? PublicKey { get; }

        Task SubscribeAsync(int userId, PushSubscriptionDto dto, string? userAgent);
        Task<bool> UnsubscribeAsync(int userId, string endpoint);

        Task<PushSendResultDto> SendToUserAsync(int userId, PushMessageDto message);
        Task<PushSendResultDto> SendToAllAsync(PushMessageDto message);

        /// <summary>„Új film érkezett" értesítés minden feliratkozónak (Hangfire háttérfeladatként fut).</summary>
        Task NotifyNewMovieAsync(int movieId);
    }
}
