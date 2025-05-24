namespace MovieShop.Server.DTOs
{
    public class AuthResultDto
    {
        public string Token { get; set; } = string.Empty;
        public UserDto User { get; set; } = null!;

        public DateTime TokenExpiration { get; set; }
    }
}
