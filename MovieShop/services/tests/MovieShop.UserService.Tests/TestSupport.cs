using System.Security.Claims;
using System.Security.Cryptography;
using AutoMapper;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieShop.UserService.Data;
using MovieShop.UserService.Mappings;
using MovieShop.UserService.Models;

namespace MovieShop.UserService.Tests;

/// <summary>
/// Valódi ASP.NET Core Identity memóriabeli adatbázissal; csak az üzenetküldés helyettesített.
/// </summary>
public sealed class IdentityHost : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly string _dbName = Guid.NewGuid().ToString();

    public IPublishEndpoint Publish { get; } = Substitute.For<IPublishEndpoint>();
    public IConfiguration Configuration { get; }

    public IdentityHost(Dictionary<string, string?>? extraConfig = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "TestOnly_0123456789abcdef0123456789abcdef_MovieShop",
            ["Jwt:Issuer"] = "MovieShop",
            ["Jwt:Audience"] = "MovieShopClient"
        };
        foreach (var (k, v) in extraConfig ?? []) values[k] = v;
        Configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton(Configuration);
        services.AddDbContext<UsersDbContext>(o => o.UseInMemoryDatabase(_dbName));
        services.AddIdentityCore<User>()
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<UsersDbContext>()
            .AddDefaultTokenProviders();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
    }

    public UserManager<User> Users => _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    public RoleManager<IdentityRole<int>> Roles => _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    public UsersDbContext Db => _scope.ServiceProvider.GetRequiredService<UsersDbContext>();

    public UsersDbContext NewDb() =>
        new(new DbContextOptionsBuilder<UsersDbContext>().UseInMemoryDatabase(_dbName).Options);

    public async Task<User> CreateUserAsync(string name, string email, string password = "Test123!", string? role = null)
    {
        var user = new User { UserName = name, Email = email, EmailConfirmed = true };
        var result = await Users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(",", result.Errors.Select(e => e.Description)));
        if (role != null)
        {
            if (!await Roles.RoleExistsAsync(role)) await Roles.CreateAsync(new IdentityRole<int>(role));
            await Users.AddToRoleAsync(user, role);
        }
        return user;
    }

    public static IMapper Mapper() => new MapperConfiguration(c => c.AddProfile<MappingProfile>()).CreateMapper();

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }
}

public static class ControllerUser
{
    public static T As<T>(this T controller, int userId, string role = "User", string? userAgent = null) where T : ControllerBase
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(ClaimTypes.Role, role)], "Test"))
        };
        if (userAgent != null) http.Request.Headers.UserAgent = userAgent;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    public static T Anonymous<T>(this T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };
        return controller;
    }

    public static object? Field(this IActionResult result, string name)
    {
        var value = ((ObjectResult)result).Value!;
        return value.GetType().GetProperty(name)!.GetValue(value);
    }

    public static int Status(this IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => throw new InvalidOperationException(result.GetType().Name)
    };
}

public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public static FakeHttpHandler Json(string json, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(_respond(request));
    }
}

/// <summary>P-256 kulcspárok a Web Push (VAPID és a böngésző feliratkozási kulcsa) teszteléséhez.</summary>
public static class PushKeys
{
    private static string B64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static (string PublicKey, string PrivateKey) NewKeyPair()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(includePrivateParameters: true);
        var publicKey = new byte[65];
        publicKey[0] = 0x04;
        p.Q.X!.CopyTo(publicKey, 1);
        p.Q.Y!.CopyTo(publicKey, 33);
        return (B64Url(publicKey), B64Url(p.D!));
    }

    public static string NewAuthSecret() => B64Url(RandomNumberGenerator.GetBytes(16));
}
