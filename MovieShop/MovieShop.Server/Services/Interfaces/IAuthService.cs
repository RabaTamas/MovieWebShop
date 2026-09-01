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

        // 2FA
        Task<TwoFactorSetupDto> GetTwoFactorSetupAsync(int userId);
        Task<IEnumerable<string>> EnableTwoFactorAsync(int userId, string code);
        Task DisableTwoFactorAsync(int userId, string code);
        Task<AuthResultDto> TwoFactorLoginAsync(string twoFactorUserId, string code);
        Task<bool> GetTwoFactorStatusAsync(int userId);
    }
}
