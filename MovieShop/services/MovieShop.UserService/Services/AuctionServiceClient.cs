namespace MovieShop.UserService.Services;

/// <summary>
/// A felhasználótörlés előfeltételének ellenőrzése: van-e a felhasználónak licitje.
/// A monolitban ezt az adatbázis Restrict idegen kulcsa kényszerítette ki; itt a
/// licitek az Auction Service adatbázisában vannak, ezért szinkron kérdés kell.
/// </summary>
public interface IAuctionServiceClient
{
    Task<bool> HasBidsAsync(int userId);
}

public class AuctionServiceClient : IAuctionServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuctionServiceClient> _logger;

    public AuctionServiceClient(HttpClient httpClient, ILogger<AuctionServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> HasBidsAsync(int userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/internal/auctions/users/{userId}/has-bids");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Auction Service returned {Status} for bid check of user {UserId}", response.StatusCode, userId);
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<HasBidsResponse>();
            return body?.HasBids == true;
        }
        catch (Exception ex)
        {
            // Degradált működés: ha az Auction Service nem érhető el, a törlés nem blokkolódik
            _logger.LogWarning(ex, "Auction Service unavailable, bid check skipped for user {UserId}", userId);
            return false;
        }
    }

    private record HasBidsResponse(bool HasBids);
}
