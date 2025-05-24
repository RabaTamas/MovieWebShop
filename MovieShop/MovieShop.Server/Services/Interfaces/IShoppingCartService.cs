using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IShoppingCartService
    {
        Task<ShoppingCartDto?> GetCartByUserIdAsync(int userId);
        Task<bool> AddToCartAsync(int userId, int movieId, int quantity);
        Task<bool> UpdateCartItemQuantityAsync(int userId, int movieId, int quantity);
        Task<bool> RemoveFromCartAsync(int userId, int movieId);
        Task<bool> ClearCartAsync(int userId);
    }
}
