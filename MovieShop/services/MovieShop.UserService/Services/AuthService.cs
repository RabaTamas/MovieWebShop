using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Google.Apis.Auth;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using MovieShop.Contracts.Events;
using MovieShop.UserService.Constants;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Models;

namespace MovieShop.UserService.Services;

/// <summary>
/// Ez az EGYETLEN hely az egész rendszerben, ahol JWT token keletkezik.
/// A többi service csak validál — ugyanazzal a szimmetrikus kulccsal, de
/// kiállítani egyikük sem tud.
///
/// A metódusok viselkedése és üzenetei a monolit AuthService-ével egyeznek;
/// a különbség csak a UserChanged események publikálása.
/// </summary>
public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly IPublishEndpoint _publishEndpoint;

    public AuthService(
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        IMapper mapper,
        IConfiguration configuration,
        IPublishEndpoint publishEndpoint)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _mapper = mapper;
        _configuration = configuration;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<AuthResultDto> RegisterAsync(UserRegisterDto registerDto)
    {
        var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
        if (existingUser != null)
            throw new ApplicationException("Email is already registered");

        var user = new User
        {
            UserName = registerDto.Name,
            Email = registerDto.Email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, registerDto.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new ApplicationException($"Failed to create user: {errors}");
        }

        if (!await _roleManager.RoleExistsAsync(UserRoles.User))
            await _roleManager.CreateAsync(new IdentityRole<int>(UserRoles.User));

        await _userManager.AddToRoleAsync(user, UserRoles.User);

        // Az Auction, Catalog és Order Service ebből tölti fel a UserSnapshot tábláját
        await PublishUserChangedAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        // A monolit regisztrációkor nem tölti ki a UserDto.Role mezőt (a leképezés
        // ignorálja) — a szerepkör a tokenben van. A válasz ezzel azonos.
        return new AuthResultDto
        {
            Token = GenerateJwtToken(user, roles),
            User = _mapper.Map<UserDto>(user),
            TokenExpiration = DateTime.UtcNow.AddDays(7)
        };
    }

    public async Task<AuthResultDto> LoginAsync(UserLoginDto loginDto)
    {
        var user = await _userManager.FindByEmailAsync(loginDto.Email);

        if (user == null || !await _userManager.CheckPasswordAsync(user, loginDto.Password))
            throw new ApplicationException("Invalid email or password");

        // 2FA esetén a jelszó helyessége még nem elég a token kiadásához
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return new AuthResultDto
            {
                RequiresTwoFactor = true,
                TwoFactorUserId = user.Id.ToString()
            };
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = _mapper.Map<UserDto>(user);
        userDto.Role = roles.FirstOrDefault() ?? "";

        return new AuthResultDto
        {
            Token = GenerateJwtToken(user, roles),
            User = userDto,
            TokenExpiration = DateTime.UtcNow.AddDays(7)
        };
    }

    public async Task<AuthResultDto> GoogleLoginAsync(string idToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Authentication:Google:ClientId"] }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings)
                ?? throw new ApplicationException("Invalid Google token");

            var user = await _userManager.FindByEmailAsync(payload.Email);

            if (user == null)
            {
                user = new User
                {
                    UserName = payload.Email.Split('@')[0],
                    Email = payload.Email,
                    EmailConfirmed = true // a Google-fiókok már hitelesítettek
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new ApplicationException($"Failed to create user: {errors}");
                }

                await _userManager.AddToRoleAsync(user, UserRoles.User);
                await PublishUserChangedAsync(user);
            }

            var roles = await _userManager.GetRolesAsync(user);

            // A monolithoz hasonlóan a Role itt sincs kitöltve a válaszban
            return new AuthResultDto
            {
                Token = GenerateJwtToken(user, roles),
                User = _mapper.Map<UserDto>(user),
                TokenExpiration = DateTime.UtcNow.AddDays(7)
            };
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Google login failed: {ex.Message}");
        }
    }

    public async Task<bool> AssignRoleAsync(int userId, string role)
    {
        if (!await _roleManager.RoleExistsAsync(role))
            await _roleManager.CreateAsync(new IdentityRole<int>(role));

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return false;

        var existingRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, existingRoles);
        await _userManager.AddToRoleAsync(user, role);

        return true;
    }

    // ── Kétfaktoros hitelesítés (TOTP, RFC 6238) ──────────────────────────────

    public async Task<TwoFactorSetupDto> GetTwoFactorSetupAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new ApplicationException("User not found");

        await _userManager.ResetAuthenticatorKeyAsync(user);
        var key = await _userManager.GetAuthenticatorKeyAsync(user)
            ?? throw new ApplicationException("Failed to generate authenticator key");

        var email = user.Email ?? user.UserName ?? "user";
        const string issuer = "MovieShop";
        var uri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}" +
                  $"?secret={key}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";

        return new TwoFactorSetupDto { SharedKey = FormatKey(key), AuthenticatorUri = uri };
    }

    public async Task<IEnumerable<string>> EnableTwoFactorAsync(int userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new ApplicationException("User not found");

        if (!await VerifyTotpAsync(user, code))
            throw new ApplicationException("Invalid verification code");

        await _userManager.SetTwoFactorEnabledAsync(user, true);

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        return recoveryCodes ?? Enumerable.Empty<string>();
    }

    public async Task DisableTwoFactorAsync(int userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new ApplicationException("User not found");

        if (!await VerifyTotpAsync(user, code))
            throw new ApplicationException("Invalid verification code");

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
    }

    public async Task<AuthResultDto> TwoFactorLoginAsync(string twoFactorUserId, string code)
    {
        var user = await _userManager.FindByIdAsync(twoFactorUserId)
            ?? throw new ApplicationException("User not found");

        if (!await VerifyTotpAsync(user, code))
            throw new ApplicationException("Invalid authentication code");

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = _mapper.Map<UserDto>(user);
        userDto.Role = roles.FirstOrDefault() ?? "";

        return new AuthResultDto
        {
            Token = GenerateJwtToken(user, roles),
            User = userDto,
            TokenExpiration = DateTime.UtcNow.AddDays(7)
        };
    }

    public async Task<bool> GetTwoFactorStatusAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new ApplicationException("User not found");

        return await _userManager.GetTwoFactorEnabledAsync(user);
    }

    // ── Segédmetódusok ────────────────────────────────────────────────────────

    private Task<bool> VerifyTotpAsync(User user, string code)
        => _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            code.Replace(" ", "").Replace("-", ""));

    private async Task PublishUserChangedAsync(User user)
    {
        await _publishEndpoint.Publish(new UserChanged
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            IsDeleted = false
        });
    }

    /// <summary>Base32 kulcs olvasható, négyes csoportokra tördelt formában.</summary>
    private static string FormatKey(string key)
    {
        var result = new StringBuilder();
        for (var i = 0; i < key.Length; i++)
        {
            if (i > 0 && i % 4 == 0) result.Append(' ');
            result.Append(key[i]);
        }
        return result.ToString().ToUpperInvariant();
    }

    private string GenerateJwtToken(User user, IList<string> roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(
            _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"]
        };

        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
    }
}
