using MovieShop.UserService.DTOs;

namespace MovieShop.UserService.Services;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(UserRegisterDto registerDto);
    Task<AuthResultDto> LoginAsync(UserLoginDto loginDto);
    Task<AuthResultDto> GoogleLoginAsync(string idToken);
    Task<bool> AssignRoleAsync(int userId, string role);

    Task<TwoFactorSetupDto> GetTwoFactorSetupAsync(int userId);
    Task<IEnumerable<string>> EnableTwoFactorAsync(int userId, string code);
    Task DisableTwoFactorAsync(int userId, string code);
    Task<AuthResultDto> TwoFactorLoginAsync(string twoFactorUserId, string code);
    Task<bool> GetTwoFactorStatusAsync(int userId);
}
