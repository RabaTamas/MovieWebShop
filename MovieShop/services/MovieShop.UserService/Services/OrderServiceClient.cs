namespace MovieShop.UserService.Services;

/// <summary>
/// A cím-rendelés kapcsolat az egyetlen hely, ahol a User Service-nek egy másik
/// service adatára van szüksége. Két admin képernyő használja (címek listája,
/// cím törlése), forgalma elhanyagolható — ezért itt a szinkron REST-hívás a
/// helyes választás, nem az eseményvezérelt replikáció.
///
/// Minden hívás hibatűrő: ha az Order Service nem elérhető, a művelet nem áll le,
/// csak a rendelésszámok maradnak nullán.
/// </summary>
public interface IOrderServiceClient
{
    Task<Dictionary<int, AddressUsage>> GetAddressUsageAsync(IEnumerable<int> addressIds);
    Task<bool> IsAddressUsedAsync(int addressId);
}

public record AddressUsage(int BillingCount, int ShippingCount);

public class OrderServiceClient : IOrderServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderServiceClient> _logger;

    public OrderServiceClient(HttpClient httpClient, ILogger<OrderServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Dictionary<int, AddressUsage>> GetAddressUsageAsync(IEnumerable<int> addressIds)
    {
        var ids = addressIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        try
        {
            var query = string.Join("&", ids.Select(id => $"addressIds={id}"));
            var response = await _httpClient.GetAsync($"/api/internal/orders/address-usage?{query}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Az Order Service {StatusCode} státusszal válaszolt a cím-felhasználás lekérdezésére",
                    response.StatusCode);
                return [];
            }

            var usages = await response.Content.ReadFromJsonAsync<List<AddressUsageResponse>>();
            return usages?.ToDictionary(u => u.AddressId, u => new AddressUsage(u.BillingCount, u.ShippingCount))
                   ?? [];
        }
        catch (Exception ex)
        {
            // Degradált működés: az admin lista betölt, a számlálók nullák maradnak.
            _logger.LogWarning(ex, "Az Order Service nem érhető el, a cím-felhasználási számlálók kimaradnak");
            return [];
        }
    }

    public async Task<bool> IsAddressUsedAsync(int addressId)
    {
        var usage = await GetAddressUsageAsync([addressId]);

        if (!usage.TryGetValue(addressId, out var counts))
        {
            // Ha nem tudjuk megállapítani, NEM tiltjuk a törlést — a rendelés
            // úgyis pillanatképként tárolja a címet, tehát nem sérül semmi.
            return false;
        }

        return counts.BillingCount > 0 || counts.ShippingCount > 0;
    }

    private record AddressUsageResponse(int AddressId, int BillingCount, int ShippingCount);
}
