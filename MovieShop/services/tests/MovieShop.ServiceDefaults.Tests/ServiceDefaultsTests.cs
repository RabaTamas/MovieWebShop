using System.Reflection;
using System.Security.Claims;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;

namespace MovieShop.ServiceDefaults.Tests;

public class ServiceDefaultsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    [Fact]
    public async Task JwtAuth_ValidatesIssuerAudienceAndKey_AndReadsHubTokenFromQuery()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddMovieShopJwtAuth(Config(("Jwt:Key", "TestOnly_0123456789abcdef0123456789abcdef")));
        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        var p = options.TokenValidationParameters;
        Assert.True(p.ValidateIssuer && p.ValidateAudience && p.ValidateLifetime && p.ValidateIssuerSigningKey);
        Assert.Equal("MovieShop", p.ValidIssuer);
        Assert.Equal("MovieShopClient", p.ValidAudience);
        Assert.IsType<SymmetricSecurityKey>(p.IssuerSigningKey);

        // SignalR: a hubok a query stringből kapják a tokent, a többi útvonal nem
        async Task<string?> TokenFor(string path)
        {
            var http = new DefaultHttpContext();
            http.Request.Path = path;
            http.Request.QueryString = new QueryString("?access_token=abc");
            var ctx = new MessageReceivedContext(http, new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), options);
            await options.Events.OnMessageReceived(ctx);
            return ctx.Token;
        }

        Assert.Equal("abc", await TokenFor("/hubs/auction"));
        Assert.Null(await TokenFor("/api/Movie"));

        var authz = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        Assert.NotNull(authz.GetPolicy("RequireAdminRole"));
        Assert.NotNull(authz.GetPolicy("RequireUserRole"));
    }

    [Fact]
    public void JwtAuth_WithoutKey_FailsFast()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddMovieShopJwtAuth(Config()));
    }

    [Theory]
    [InlineData("http://a.test, http://b.test", new[] { "http://a.test", "http://b.test" })]
    [InlineData(null, new[] { "http://localhost:3000", "https://localhost:5173" })]
    public void Cors_UsesConfiguredOriginsOrDefaults(string? origins, string[] expected)
    {
        var provider = new ServiceCollection().AddLogging()
            .AddMovieShopCors(Config(("CORS:AllowedOrigins", origins)))
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = options.GetPolicy(options.DefaultPolicyName)!;
        Assert.Equal(expected, policy.Origins);
        Assert.True(policy.SupportsCredentials);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Endpoint=sb://movieshop-test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA==")]
    public void Messaging_ConfiguresRabbitMqOrServiceBus_WithServicePrefixedQueues(string? serviceBus)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddMovieShopMessaging(
            Config(("ServiceBus:ConnectionString", serviceBus), ("RabbitMq:Host", "rabbit")),
            Assembly.GetExecutingAssembly(), "order-service");
        var provider = services.BuildServiceProvider();

        // A busz felépítése lefuttatja a teljes transzport-konfigurációt (kapcsolódás nélkül)
        var bus = provider.GetRequiredService<IBusControl>();
        Assert.NotNull(bus);

        var formatter = provider.GetRequiredService<IEndpointNameFormatter>();
        Assert.Equal("order-service-sample", formatter.Consumer<SampleConsumer>());
    }

    [Fact]
    public void Swagger_DocumentsBearerSecurity()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMovieShopSwagger("Teszt API");
        var app = builder.Build();

        var doc = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        Assert.Equal("Teszt API", doc.Info.Title);
        Assert.True(doc.Components.SecuritySchemes.ContainsKey("Bearer"));
        Assert.Single(doc.SecurityRequirements);
    }

    [Fact]
    public async Task HealthAndMetricsEndpoints_AreMapped()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHealthChecks();
        var app = builder.Build();
        app.MapMovieShopHealthChecks();
        app.MapMovieShopMetrics();
        await app.StartAsync();

        try
        {
            var address = app.Urls.First();
            using var http = new HttpClient { BaseAddress = new Uri(address) };

            Assert.Equal("Healthy", await http.GetStringAsync("/health"));
            Assert.Contains("http_requests_received_total", await http.GetStringAsync("/metrics"));
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public void CurrentUser_ReadsIdNameAndRoleFromClaims()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "42"), new Claim(ClaimTypes.Name, "neo"), new Claim(ClaimTypes.Role, "Admin")], "Test"));
        var viaSub = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "7")], "Test"));
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Equal(42, user.GetUserId());
        Assert.Equal("neo", user.GetUserName());
        Assert.True(user.IsAdmin());
        Assert.Equal(7, viaSub.GetUserId());
        Assert.Equal(0, anonymous.GetUserId());
        Assert.Equal("Ismeretlen", anonymous.GetUserName());
        Assert.False(anonymous.IsAdmin());
    }

    public record SampleMessage;

    public class SampleConsumer : IConsumer<SampleMessage>
    {
        public Task Consume(ConsumeContext<SampleMessage> context) => Task.CompletedTask;
    }
}
