using MovieShop.Server.DTOs;
using MovieShop.Server.Models;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResultDto> RegisterAsync(UserRegisterDto registerDto);
        Task<AuthResultDto> LoginAsync(UserLoginDto loginDto);
        Task<AuthResultDto> GoogleLoginAsync(string idToken);
        Task<bool> IsAdminAsync(int userId);
        Task<bool> AssignRoleAsync(int userId, string role);
        string GenerateJwtToken(User user, IList<string> roles);
    }
}
