using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieShop.Contracts.Events;
using MovieShop.UserService.Constants;
using MovieShop.UserService.Data;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Mappings;
using MovieShop.UserService.Models;
using MovieShop.UserService.Services;

namespace MovieShop.UserService.Tests;

/// <summary>
/// Valódi ASP.NET Core Identity (jelszó-hashelés, TOTP-ellenőrzés) memóriabeli
/// adatbázissal. Csak a MassTransit publikálás van helyettesítve.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private const string Password = "Test123!";

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly IPublishEndpoint _publish = Substitute.For<IPublishEndpoint>();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TestOnly_0123456789abcdef0123456789abcdef_MovieShop",
                ["Jwt:Issuer"] = "MovieShop",
                ["Jwt:Audience"] = "MovieShopClient"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<UsersDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<User>()
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<UsersDbContext>()
            .AddDefaultTokenProviders();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();

        _auth = new AuthService(
            _scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
            _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>(),
            mapper,
            configuration,
            _publish);
    }

    private Task<AuthResultDto> RegisterAsync(string email = "neo@zion.io")
        => _auth.RegisterAsync(new UserRegisterDto { Name = "neo", Email = email, Password = Password });

    private static JwtSecurityToken Decode(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public async Task Register_IssuesJwtWithUserRole_AndPublishesUserChanged()
    {
        var result = await RegisterAsync();

        var jwt = Decode(result.Token);
        Assert.Equal("MovieShop", jwt.Issuer);
        Assert.Contains("MovieShopClient", jwt.Audiences);
        Assert.Contains(jwt.Claims, c => c.Type == "email" && c.Value == "neo@zion.io");
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == UserRoles.User);
        Assert.Equal(result.User.Id.ToString(), jwt.Claims.First(c => c.Type == "nameid").Value);

        await _publish.Received(1).Publish(
            Arg.Is<UserChanged>(e => e.UserId == result.User.Id && e.Email == "neo@zion.io" && !e.IsDeleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws()
    {
        await RegisterAsync();

        var ex = await Assert.ThrowsAsync<ApplicationException>(() => RegisterAsync());
        Assert.Equal("Email is already registered", ex.Message);
    }

    [Fact]
    public async Task Register_WeakPassword_IsRejectedByIdentityPolicy()
    {
        var ex = await Assert.ThrowsAsync<ApplicationException>(() =>
            _auth.RegisterAsync(new UserRegisterDto { Name = "neo", Email = "neo@zion.io", Password = "abc" }));

        Assert.StartsWith("Failed to create user", ex.Message);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsTokenAndRole()
    {
        await RegisterAsync();

        var result = await _auth.LoginAsync(new UserLoginDto { Email = "neo@zion.io", Password = Password });

        Assert.False(result.RequiresTwoFactor);
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal(UserRoles.User, result.User.Role);
    }

    [Theory]
    [InlineData("neo@zion.io", "Wrong123!")]
    [InlineData("nobody@zion.io", Password)]
    public async Task Login_WithBadCredentials_ThrowsSameMessage(string email, string password)
    {
        await RegisterAsync();

        // Ugyanaz az üzenet, hogy ne lehessen kitalálni, létezik-e a fiók
        var ex = await Assert.ThrowsAsync<ApplicationException>(() =>
            _auth.LoginAsync(new UserLoginDto { Email = email, Password = password }));
        Assert.Equal("Invalid email or password", ex.Message);
    }

    [Fact]
    public async Task TwoFactor_FullFlow_WithRealTotpCode()
    {
        var userId = (await RegisterAsync()).User.Id;

        // 1. Beállítás: megosztott kulcs + otpauth:// URI a QR-kódhoz
        var setup = await _auth.GetTwoFactorSetupAsync(userId);
        Assert.StartsWith("otpauth://totp/MovieShop:", setup.AuthenticatorUri);

        // 2. Bekapcsolás az authenticator által mutatott kóddal
        var recoveryCodes = await _auth.EnableTwoFactorAsync(userId, Totp.Compute(setup.SharedKey));
        Assert.Equal(8, recoveryCodes.Count());
        Assert.True(await _auth.GetTwoFactorStatusAsync(userId));

        // 3. Jelszavas belépés már nem ad tokent
        var first = await _auth.LoginAsync(new UserLoginDto { Email = "neo@zion.io", Password = Password });
        Assert.True(first.RequiresTwoFactor);
        Assert.True(string.IsNullOrEmpty(first.Token));
        Assert.Equal(userId.ToString(), first.TwoFactorUserId);

        // 4. A második lépés a TOTP-kóddal adja ki a tokent
        var second = await _auth.TwoFactorLoginAsync(first.TwoFactorUserId!, Totp.Compute(setup.SharedKey));
        Assert.False(string.IsNullOrEmpty(second.Token));
    }

    [Fact]
    public async Task TwoFactor_WrongCode_IsRejected()
    {
        var userId = (await RegisterAsync()).User.Id;
        var setup = await _auth.GetTwoFactorSetupAsync(userId);
        await _auth.EnableTwoFactorAsync(userId, Totp.Compute(setup.SharedKey));

        var wrong = Totp.Compute(setup.SharedKey, DateTimeOffset.UtcNow.AddHours(-1));

        var ex = await Assert.ThrowsAsync<ApplicationException>(() =>
            _auth.TwoFactorLoginAsync(userId.ToString(), wrong));
        Assert.Equal("Invalid authentication code", ex.Message);
    }

    [Fact]
    public async Task TwoFactor_EnableWithWrongCode_LeavesItDisabled()
    {
        var userId = (await RegisterAsync()).User.Id;
        await _auth.GetTwoFactorSetupAsync(userId);

        await Assert.ThrowsAsync<ApplicationException>(() => _auth.EnableTwoFactorAsync(userId, "000000"));
        Assert.False(await _auth.GetTwoFactorStatusAsync(userId));
    }

    [Fact]
    public async Task AssignRole_ReplacesExistingRole()
    {
        var userId = (await RegisterAsync()).User.Id;

        Assert.True(await _auth.AssignRoleAsync(userId, UserRoles.Admin));

        var login = await _auth.LoginAsync(new UserLoginDto { Email = "neo@zion.io", Password = Password });
        Assert.Equal(UserRoles.Admin, login.User.Role);
        Assert.DoesNotContain(Decode(login.Token).Claims, c => c.Type == "role" && c.Value == UserRoles.User);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}
