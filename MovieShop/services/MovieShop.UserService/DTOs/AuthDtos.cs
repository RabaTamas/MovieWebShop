using System.ComponentModel.DataAnnotations;

namespace MovieShop.UserService.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UserLoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UserRegisterDto
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AuthResultDto
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
    public DateTime TokenExpiration { get; set; }

    /// <summary>Ha igaz, a jelszó helyes volt, de a TOTP-lépés még hátravan — nincs kiadott token.</summary>
    public bool RequiresTwoFactor { get; set; }
    public string? TwoFactorUserId { get; set; }
}

public class GoogleLoginDto
{
    public string IdToken { get; set; } = string.Empty;
}

public class UserProfileDto
{
    public string Email { get; set; } = string.Empty;
}

public class UpdateEmailDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string NewEmail { get; set; } = string.Empty;
}

public class UpdateDisplayNameDto
{
    public string NewName { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    [Required(ErrorMessage = "The current password is required.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Entering a new password is mandatory.")]
    [MinLength(8, ErrorMessage = "The new password must be at least 8 characters long.")]
    public string NewPassword { get; set; } = string.Empty;
}

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
