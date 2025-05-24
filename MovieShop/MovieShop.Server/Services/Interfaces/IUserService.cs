using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserProfileDto?> GetProfileAsync(int userId);
        Task<bool> UpdateEmailAsync(int userId, UpdateEmailDto dto);
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordDto dto);

        Task<List<UserDto>> GetAllUsersAsync();

        Task<bool> DeleteUserAsync(int userId);
    }
}