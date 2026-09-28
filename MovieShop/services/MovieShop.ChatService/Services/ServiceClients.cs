using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MovieShop.ChatService.Models;

namespace MovieShop.ChatService.Services;

/// <summary>
/// A Chat Service az egyetlen, amelynek NINCS saját adatbázisa — minden adatot
/// más service-ektől kér el. A monolit ChatService és AgentService közvetlen
/// DbContext-lekérdezései itt REST-hívásokká váltak.
///
/// A hívások a FELHASZNÁLÓ NEVÉBEN mennek: a bejövő JWT tokent továbbadjuk,
/// így a hívott service ugyanazokat a jogosultsági szabályokat érvényesíti,
/// mintha a böngésző hívta volna közvetlenül.
/// </summary>
public interface IServiceClients
{
    Task<List<CatalogMovie>> GetCatalogAsync();
    Task<List<CategoryItem>> GetCategoriesAsync();
    Task<UserOrderContext?> GetUserContextAsync(int userId);
    Task<List<TopMovie>> GetTopMoviesAsync(int count = 5);
    Task<UserInfo?> GetUserInfoAsync(int userId);

    Task<bool> AddToCartAsync(int movieId);
    Task<bool> RemoveFromCartAsync(int movieId);
    Task<(bool Success, string? Error)> UpdateDisplayNameAsync(string newName);
    Task<List<AddressInfo>> GetAddressesAsync();
    Task<bool> CreateAddressAsync(string street, string city, string zip);
    Task<bool> UpdateAddressAsync(int addressId, string street, string city, string zip);
}

public class ServiceClients : IServiceClients
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _config;
    private readonly ILogger<ServiceClients> _logger;

    public ServiceClients(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration config,
        ILogger<ServiceClients> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _config = config;
        _logger = logger;
    }

    // ── Catalog Service ───────────────────────────────────────────────────────

    public async Task<List<CatalogMovie>> GetCatalogAsync()
        => await GetAsync<List<CatalogMovie>>("Catalog", "/api/internal/catalog/movies") ?? [];

    public async Task<List<CategoryItem>> GetCategoriesAsync()
        => await GetAsync<List<CategoryItem>>("Catalog", "/api/Category") ?? [];

    // ── Order Service ─────────────────────────────────────────────────────────

    public Task<UserOrderContext?> GetUserContextAsync(int userId)
        => GetAsync<UserOrderContext>("Order", $"/api/internal/orders/context/{userId}");

    public async Task<List<TopMovie>> GetTopMoviesAsync(int count = 5)
        => await GetAsync<List<TopMovie>>("Order", $"/api/internal/orders/top-movies?count={count}") ?? [];

    public async Task<bool> AddToCartAsync(int movieId)
        => (await SendAsync("Order", HttpMethod.Post, "/api/ShoppingCart/add", new { movieId, quantity = 1 })).Success;

    public async Task<bool> RemoveFromCartAsync(int movieId)
        => (await SendAsync("Order", HttpMethod.Delete, $"/api/ShoppingCart/remove/{movieId}", null)).Success;

    // ── User Service ──────────────────────────────────────────────────────────

    public async Task<UserInfo?> GetUserInfoAsync(int userId)
        => (await GetAsync<List<UserInfo>>("User", $"/api/internal/users/lookup?userIds={userId}"))?.FirstOrDefault();

    public async Task<(bool Success, string? Error)> UpdateDisplayNameAsync(string newName)
    {
        var (success, body) = await SendAsync("User", HttpMethod.Put, "/api/User/display-name", new { newName });
        return success ? (true, null) : (false, ExtractMessage(body));
    }

    public async Task<List<AddressInfo>> GetAddressesAsync()
        => await GetAsync<List<AddressInfo>>("User", "/api/Address") ?? [];

    public async Task<bool> CreateAddressAsync(string street, string city, string zip)
        => (await SendAsync("User", HttpMethod.Post, "/api/Address", new { street, city, zip })).Success;

    public async Task<bool> UpdateAddressAsync(int addressId, string street, string city, string zip)
        => (await SendAsync("User", HttpMethod.Put, $"/api/Address/{addressId}", new { id = addressId, street, city, zip })).Success;

    // ── Közös HTTP-réteg ──────────────────────────────────────────────────────

    private HttpClient CreateClient(string service)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_config[$"Services:{service}Service"]
            ?? throw new InvalidOperationException($"Services:{service}Service is not configured"));
        client.Timeout = TimeSpan.FromSeconds(10);

        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);

        return client;
    }

    private async Task<T?> GetAsync<T>(string service, string path)
    {
        try
        {
            using var client = CreateClient(service);
            var response = await client.GetAsync(path);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("{Service}{Path} -> {Status}", service, path, response.StatusCode);
                return default;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            // A chatbot degradáltan is működjön: hiányzó kontextussal is válaszol.
            _logger.LogWarning(ex, "Call to {Service} service failed: {Path}", service, path);
            return default;
        }
    }

    private async Task<(bool Success, string? Body)> SendAsync(string service, HttpMethod method, string path, object? payload)
    {
        try
        {
            using var client = CreateClient(service);
            using var request = new HttpRequestMessage(method, path);

            if (payload != null)
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("{Method} {Service}{Path} -> {Status}", method, service, path, response.StatusCode);

            return (response.IsSuccessStatusCode, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call to {Service} service failed: {Path}", service, path);
            return (false, ex.Message);
        }
    }

    private static string? ExtractMessage(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return body;

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : body;
        }
        catch
        {
            return body;
        }
    }
}
