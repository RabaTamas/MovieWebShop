using System.Net.Http.Headers;
using MovieShop.OrderService.DTOs;

namespace MovieShop.OrderService.Services;

/// <summary>
/// A monolit rendeléskor a számlázási címet új Address rekordként elmentette a
/// felhasználó címei közé (a kliens azonosító nélkül küldi). A címek a User Service-ben
/// élnek, ezért ez itt szinkron REST-hívás a felhasználó tokenjével.
/// </summary>
public interface IUserServiceClient
{
    Task<AddressDto?> CreateAddressAsync(AddressDto address);
    Task DeleteAddressAsync(int addressId);
}

public class UserServiceClient : IUserServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserServiceClient> _logger;

    public UserServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<AddressDto?> CreateAddressAsync(AddressDto address)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Address")
            {
                Content = JsonContent.Create(new { street = address.Street, city = address.City, zip = address.Zip })
            };
            ForwardToken(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("User Service returned {Status} when creating billing address", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AddressDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create billing address in User Service");
            return null;
        }
    }

    public async Task DeleteAddressAsync(int addressId)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/Address/{addressId}");
            ForwardToken(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("User Service returned {Status} when deleting address {AddressId}", response.StatusCode, addressId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete address {AddressId} in User Service", addressId);
        }
    }

    private void ForwardToken(HttpRequestMessage request)
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
    }
}
