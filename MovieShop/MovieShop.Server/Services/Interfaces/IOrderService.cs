using MovieShop.Server.Controllers;
using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IOrderService
    {
        Task<IEnumerable<OrderDto>> GetOrdersByUserIdAsync(int userId);
        Task<OrderDto?> GetOrderByIdAsync(int orderId, int userId);
        Task<OrderDto?> CreateOrderFromCartAsync(int userId, OrderRequestDto dto);
        Task<bool> CancelOrderAsync(int orderId, int userId);

        Task<IEnumerable<OrderDto>> GetAllOrdersAsync();
        Task<OrderDto?> GetOrderByIdAdminAsync(int orderId);
        Task<bool> UpdateOrderStatusAsync(int orderId, string status);
        Task<IEnumerable<OrderDto>> GetOrdersByStatusAsync(string status);
        Task<OrderStatisticsDto> GetOrderStatisticsAsync();
        Task<bool> HasUserPurchasedMovieAsync(int userId, int movieId);
        Task<IEnumerable<int>> GetPurchasedMovieIdsAsync(int userId);
    }
}
