namespace MovieShop.Server.DTOs
{
    public class TwoFactorSetupDto
    {
        public string SharedKey { get; set; } = string.Empty;
        public string AuthenticatorUri { get; set; } = string.Empty;
    }

    public class EnableTwoFactorDto
    {
        public string Code { get; set; } = string.Empty;
    }

    public class TwoFactorLoginDto
    {
        public string TwoFactorUserId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class TwoFactorStatusDto
    {
        public bool IsEnabled { get; set; }
        public IEnumerable<string>? RecoveryCodes { get; set; }
    }
}
