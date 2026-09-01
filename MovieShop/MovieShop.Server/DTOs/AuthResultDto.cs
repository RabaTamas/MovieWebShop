namespace MovieShop.Server.DTOs
{
    public class AuthResultDto
    {
        public string Token { get; set; } = string.Empty;
        public UserDto User { get; set; } = null!;
        public DateTime TokenExpiration { get; set; }

        // Set when 2FA is required instead of issuing a JWT directly
        public bool RequiresTwoFactor { get; set; }
        public string? TwoFactorUserId { get; set; }
    }
}
