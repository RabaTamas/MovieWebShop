using System.Net;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.Contracts.Events;
using MovieShop.UserService.Consumers;
using MovieShop.UserService.Data;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Extensions;
using MovieShop.UserService.Models;
using MovieShop.UserService.Services;

namespace MovieShop.UserService.Tests;

public class AuthServiceExtraTests : IDisposable
{
    private readonly IdentityHost _host = new(new() { ["Authentication:Google:ClientId"] = "client-id" });

    private AuthService Create() => new(_host.Users, _host.Roles, IdentityHost.Mapper(), _host.Configuration, _host.Publish);

    [Fact]
    public async Task GoogleLogin_WithInvalidToken_IsRejected()
    {
        var ex = await Assert.ThrowsAsync<ApplicationException>(() => Create().GoogleLoginAsync("nem-valodi-token"));
        Assert.StartsWith("Google login failed", ex.Message);
    }

    [Fact]
    public async Task AssignRole_CreatesMissingRole_AndUnknownUserIsFalse()
    {
        var user = await _host.CreateUserAsync("neo", "neo@zion.io");

        Assert.True(await Create().AssignRoleAsync(user.Id, "Moderator"));
        Assert.True(await _host.Roles.RoleExistsAsync("Moderator"));
        Assert.False(await Create().AssignRoleAsync(999, "Admin"));
    }

    [Fact]
    public async Task TwoFactor_DisableWithCode_AndUnknownUsersThrow()
    {
        var user = await _host.CreateUserAsync("neo", "neo@zion.io");
        var auth = Create();
        var setup = await auth.GetTwoFactorSetupAsync(user.Id);
        Assert.Matches("^[A-Z0-9 ]+$", setup.SharedKey);
        await auth.EnableTwoFactorAsync(user.Id, Totp.Compute(setup.SharedKey));

        await Assert.ThrowsAsync<ApplicationException>(() => auth.DisableTwoFactorAsync(user.Id, "000000"));
        await auth.DisableTwoFactorAsync(user.Id, Totp.Compute(setup.SharedKey).Insert(3, " "));
        Assert.False(await auth.GetTwoFactorStatusAsync(user.Id));

        await Assert.ThrowsAsync<ApplicationException>(() => auth.GetTwoFactorSetupAsync(999));
        await Assert.ThrowsAsync<ApplicationException>(() => auth.EnableTwoFactorAsync(999, "1"));
        await Assert.ThrowsAsync<ApplicationException>(() => auth.DisableTwoFactorAsync(999, "1"));
        await Assert.ThrowsAsync<ApplicationException>(() => auth.TwoFactorLoginAsync("999", "1"));
        await Assert.ThrowsAsync<ApplicationException>(() => auth.GetTwoFactorStatusAsync(999));
    }

    [Fact]
    public async Task Register_ReusesExistingUserRole()
    {
        await _host.Roles.CreateAsync(new IdentityRole<int>("User"));

        var result = await Create().RegisterAsync(new UserRegisterDto { Name = "neo", Email = "neo@zion.io", Password = "Test123!" });

        Assert.False(string.IsNullOrEmpty(result.Token));
    }

    [Fact]
    public async Task Token_RequiresConfiguredKey()
    {
        var noKey = new AuthService(_host.Users, _host.Roles, IdentityHost.Mapper(),
            new ConfigurationBuilder().Build(), _host.Publish);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            noKey.RegisterAsync(new UserRegisterDto { Name = "x", Email = "x@x.hu", Password = "Test123!" }));
    }

    public void Dispose() => _host.Dispose();
}

public class ProfileServiceTests : IDisposable
{
    private readonly IdentityHost _host = new();
    private readonly IAuctionServiceClient _auctions = Substitute.For<IAuctionServiceClient>();

    private ProfileService Create() => new(_host.Users, _host.Publish, _auctions);

    [Fact]
    public async Task Profile_AndAllUsersWithRoles()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        await _host.CreateUserAsync("admin", "admin@zion.io", role: "Admin");

        Assert.Equal("neo@zion.io", (await Create().GetProfileAsync(neo.Id))!.Email);
        Assert.Null(await Create().GetProfileAsync(999));

        var all = await Create().GetAllUsersAsync();
        Assert.Equal("User", all.Single(u => u.Name == "neo").Role);
        Assert.Equal("Admin", all.Single(u => u.Name == "admin").Role);
    }

    [Fact]
    public async Task UpdateEmail_ChangesLoginName_PublishesEvent_AndRejectsTakenEmail()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        await _host.CreateUserAsync("trinity", "trinity@zion.io");

        Assert.False(await Create().UpdateEmailAsync(neo.Id, new UpdateEmailDto { NewEmail = "trinity@zion.io" }));
        Assert.False(await Create().UpdateEmailAsync(999, new UpdateEmailDto { NewEmail = "x@x.hu" }));
        Assert.True(await Create().UpdateEmailAsync(neo.Id, new UpdateEmailDto { NewEmail = "the.one@zion.io" }));

        Assert.NotNull(await _host.Users.FindByEmailAsync("the.one@zion.io"));
        await _host.Publish.Received(1).Publish(Arg.Is<UserChanged>(e => e.Email == "the.one@zion.io"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrentPassword()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");

        Assert.False(await Create().ChangePasswordAsync(neo.Id, new ChangePasswordDto { CurrentPassword = "rossz", NewPassword = "New123!x" }));
        Assert.True(await Create().ChangePasswordAsync(neo.Id, new ChangePasswordDto { CurrentPassword = "Test123!", NewPassword = "New123!x" }));
        Assert.False(await Create().ChangePasswordAsync(999, new ChangePasswordDto()));
    }

    [Fact]
    public async Task UpdateDisplayName_ValidatesAndPublishes()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");

        Assert.Equal((true, (string?)null), await Create().UpdateDisplayNameAsync(neo.Id, "TheOne"));
        Assert.Equal((false, "User not found"), await Create().UpdateDisplayNameAsync(999, "x"));

        var (ok, error) = await Create().UpdateDisplayNameAsync(neo.Id, "szóköz nem jó");
        Assert.False(ok);
        Assert.Contains("invalid", error);
        await _host.Publish.Received(1).Publish(Arg.Is<UserChanged>(e => e.UserName == "TheOne"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_BlockedByBids_OtherwisePublishesDeletion()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        var bidder = await _host.CreateUserAsync("bidder", "bidder@zion.io");
        _auctions.HasBidsAsync(bidder.Id).Returns(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Create().DeleteUserAsync(bidder.Id));
        Assert.True(await Create().DeleteUserAsync(neo.Id));
        Assert.False(await Create().DeleteUserAsync(999));

        await _host.Publish.Received(1).Publish(Arg.Is<UserChanged>(e => e.UserId == neo.Id && e.IsDeleted), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _host.Dispose();
}

public class AddressServiceTests : IDisposable
{
    private readonly IdentityHost _host = new();
    private readonly IOrderServiceClient _orders = Substitute.For<IOrderServiceClient>();

    private AddressService Create() => new(_host.NewDb(), IdentityHost.Mapper(), _orders);
    private AdminAddressService CreateAdmin() => new(_host.NewDb(), IdentityHost.Mapper(), _orders);

    private static AddressDto Addr(string street) => new() { Street = street, City = "Budapest", Zip = "1111" };

    [Fact]
    public async Task UserAddresses_AreIsolatedPerUser()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        var other = await _host.CreateUserAsync("other", "other@zion.io");

        var created = await Create().CreateAddressAsync(Addr("Fő utca 1."), neo.Id);

        Assert.Single(await Create().GetUserAddressesAsync(neo.Id));
        Assert.Empty(await Create().GetUserAddressesAsync(other.Id));
        Assert.Equal("Fő utca 1.", (await Create().GetAddressByIdAsync(created.Id, neo.Id)).Street);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Create().GetAddressByIdAsync(created.Id, other.Id));

        var updated = await Create().UpdateAddressAsync(created.Id, Addr("Új utca 2."), neo.Id);
        Assert.Equal("Új utca 2.", updated.Street);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Create().UpdateAddressAsync(created.Id, Addr("x"), other.Id));
    }

    [Fact]
    public async Task Delete_IsBlockedWhenUsedInOrders()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        var used = await Create().CreateAddressAsync(Addr("Használt"), neo.Id);
        var free = await Create().CreateAddressAsync(Addr("Szabad"), neo.Id);
        _orders.IsAddressUsedAsync(used.Id).Returns(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Create().DeleteAddressAsync(used.Id, neo.Id));
        Assert.True(await Create().DeleteAddressAsync(free.Id, neo.Id));
        Assert.False(await Create().DeleteAddressAsync(free.Id, neo.Id));
    }

    [Fact]
    public async Task Admin_ListsAllAddressesWithOrderCounts()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        var a = await Create().CreateAddressAsync(Addr("A"), neo.Id);
        var b = await Create().CreateAddressAsync(Addr("B"), neo.Id);
        _orders.GetAddressUsageAsync(Arg.Any<IEnumerable<int>>())
            .Returns(new Dictionary<int, AddressUsage> { [a.Id] = new(2, 1) });

        var all = await CreateAdmin().GetAllAddressesAsync();
        Assert.Equal("neo", all[0].UserName);
        Assert.Equal(2, all.Single(x => x.Id == a.Id).BillingOrdersCount);
        Assert.Equal(0, all.Single(x => x.Id == b.Id).BillingOrdersCount);

        var one = await CreateAdmin().GetAddressByIdAsync(a.Id);
        Assert.Equal(1, one.ShippingOrdersCount);
        Assert.Equal(0, (await CreateAdmin().GetAddressByIdAsync(b.Id)).BillingOrdersCount);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateAdmin().GetAddressByIdAsync(999));
    }

    [Fact]
    public async Task Admin_UpdateAndDelete()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        var a = await Create().CreateAddressAsync(Addr("A"), neo.Id);
        var b = await Create().CreateAddressAsync(Addr("B"), neo.Id);
        _orders.IsAddressUsedAsync(a.Id).Returns(true);

        Assert.Equal("A2", (await CreateAdmin().UpdateAddressAsync(a.Id, Addr("A2"))).Street);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateAdmin().UpdateAddressAsync(999, Addr("x")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAdmin().DeleteAddressAsync(a.Id));
        Assert.True(await CreateAdmin().DeleteAddressAsync(b.Id));
        Assert.False(await CreateAdmin().DeleteAddressAsync(b.Id));
    }

    public void Dispose() => _host.Dispose();
}

public class PushNotificationServiceTests : IDisposable
{
    private readonly IdentityHost _host;
    private readonly (string Public, string Private) _vapid = PushKeys.NewKeyPair();
    private readonly List<string> _delivered = [];

    public PushNotificationServiceTests()
    {
        _host = new IdentityHost();
    }

    private PushNotificationService Create(bool configured = true)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WebPush:PublicKey"] = configured ? _vapid.Public : null,
            ["WebPush:PrivateKey"] = configured ? _vapid.Private : null
        }).Build();

        // A push-szolgáltatás válasza az endpoint nevétől függ: ok / gone / error
        var handler = new FakeHttpHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            _delivered.Add(url);
            return new HttpResponseMessage(url.Contains("gone") ? HttpStatusCode.Gone
                : url.Contains("error") ? HttpStatusCode.InternalServerError
                : HttpStatusCode.Created);
        });
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("WebPush").Returns(new HttpClient(handler));

        return new PushNotificationService(_host.NewDb(), config, factory, NullLogger<PushNotificationService>.Instance);
    }

    private async Task SubscribeAsync(int userId, string endpoint)
    {
        var (clientPublic, _) = PushKeys.NewKeyPair();
        await Create().SubscribeAsync(userId, new PushSubscriptionDto
        {
            Endpoint = $"https://push.test/{endpoint}",
            Keys = new PushSubscriptionKeysDto { P256dh = clientPublic, Auth = PushKeys.NewAuthSecret() }
        }, new string('x', 400));
    }

    [Fact]
    public async Task Configuration_IsExposed()
    {
        Assert.True(Create().IsConfigured);
        Assert.Equal(_vapid.Public, Create().PublicKey);
        Assert.False(Create(configured: false).IsConfigured);
    }

    [Fact]
    public async Task Subscribe_UpsertsByEndpoint_AndTruncatesUserAgent()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        await SubscribeAsync(neo.Id, "a");
        await SubscribeAsync(neo.Id, "a");

        await using var db = _host.NewDb();
        var sub = await db.WebPushSubscriptions.SingleAsync();
        Assert.Equal(300, sub.UserAgent!.Length);
        Assert.EndsWith("…", sub.UserAgent);

        Assert.True(await Create().UnsubscribeAsync(neo.Id, "https://push.test/a"));
        Assert.False(await Create().UnsubscribeAsync(neo.Id, "https://push.test/a"));
    }

    [Fact]
    public async Task SendToAll_CountsDelivered_RemovesExpired_AndCountsFailures()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        var other = await _host.CreateUserAsync("other", "other@zion.io");
        await SubscribeAsync(neo.Id, "ok");
        await SubscribeAsync(neo.Id, "gone");
        await SubscribeAsync(other.Id, "error");

        var result = await Create().SendToAllAsync(PushNotificationService.NewMovieMessage(1, "Dune", "Arrakis", "d.jpg", 1500, 1200));

        Assert.Equal(1, result.Sent);
        Assert.Equal(1, result.Removed);
        Assert.Equal(1, result.Failed);
        await using var db = _host.NewDb();
        Assert.Equal(2, await db.WebPushSubscriptions.CountAsync());

        var onlyNeo = await Create().SendToUserAsync(other.Id, new PushMessageDto { Title = "t" });
        Assert.Equal(1, onlyNeo.Failed);
    }

    [Fact]
    public async Task Send_SkipsWhenNotConfiguredOrNoSubscribers()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        await SubscribeAsync(neo.Id, "ok");

        Assert.Equal(0, (await Create(configured: false).SendToAllAsync(new PushMessageDto())).Sent);
        Assert.Equal(0, (await Create().SendToUserAsync(999, new PushMessageDto())).Sent);
        Assert.Empty(_delivered);
    }

    [Fact]
    public void NewMovieMessage_FormatsPriceAndDescription()
    {
        var withDiscount = PushNotificationService.NewMovieMessage(5, "Dune", new string('d', 200), "", 1500, 1200);
        Assert.Equal("New movie: Dune", withDiscount.Title);
        Assert.Contains("(was", withDiscount.Body);
        Assert.Contains("…", withDiscount.Body);
        Assert.Null(withDiscount.Image);
        Assert.Equal("/movies/5", withDiscount.Url);

        var plain = PushNotificationService.NewMovieMessage(5, "Dune", " ", "d.jpg", 1500, null);
        Assert.StartsWith("Now available for", plain.Body);
        Assert.Equal("d.jpg", plain.Image);
    }

    public void Dispose() => _host.Dispose();
}

public class ServiceClientTests
{
    private static T Client<T>(FakeHttpHandler handler, Func<HttpClient, T> create)
        => create(new HttpClient(handler) { BaseAddress = new Uri("http://svc") });

    [Fact]
    public async Task OrderClient_ParsesUsage_AndDegradesOnErrors()
    {
        var ok = Client(FakeHttpHandler.Json("""[{"addressId":1,"billingCount":2,"shippingCount":0},{"addressId":2,"billingCount":0,"shippingCount":0}]"""),
            h => new OrderServiceClient(h, NullLogger<OrderServiceClient>.Instance));

        var usage = await ok.GetAddressUsageAsync([1, 2, 2]);
        Assert.Equal(2, usage[1].BillingCount);
        Assert.True(await ok.IsAddressUsedAsync(1));
        Assert.False(await ok.IsAddressUsedAsync(2));
        Assert.Empty(await ok.GetAddressUsageAsync([]));

        var failing = Client(FakeHttpHandler.Json("", HttpStatusCode.ServiceUnavailable),
            h => new OrderServiceClient(h, NullLogger<OrderServiceClient>.Instance));
        Assert.Empty(await failing.GetAddressUsageAsync([1]));
        Assert.False(await failing.IsAddressUsedAsync(1));

        var down = Client(new FakeHttpHandler(_ => throw new HttpRequestException()),
            h => new OrderServiceClient(h, NullLogger<OrderServiceClient>.Instance));
        Assert.Empty(await down.GetAddressUsageAsync([1]));
    }

    [Fact]
    public async Task AuctionClient_ReportsBids_AndDegradesOnErrors()
    {
        var yes = Client(FakeHttpHandler.Json("""{"hasBids":true}"""), h => new AuctionServiceClient(h, NullLogger<AuctionServiceClient>.Instance));
        var no = Client(FakeHttpHandler.Json("""{"hasBids":false}"""), h => new AuctionServiceClient(h, NullLogger<AuctionServiceClient>.Instance));
        var error = Client(FakeHttpHandler.Json("", HttpStatusCode.InternalServerError), h => new AuctionServiceClient(h, NullLogger<AuctionServiceClient>.Instance));
        var down = Client(new FakeHttpHandler(_ => throw new HttpRequestException()), h => new AuctionServiceClient(h, NullLogger<AuctionServiceClient>.Instance));

        Assert.True(await yes.HasBidsAsync(1));
        Assert.False(await no.HasBidsAsync(1));
        Assert.False(await error.HasBidsAsync(1));
        Assert.False(await down.HasBidsAsync(1));
    }
}

public class MovieChangedConsumerTests
{
    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 0)]
    [InlineData(true, true, 0)]
    public async Task OnlyNewActiveMovies_TriggerPush(bool isNew, bool deleted, int expectedCalls)
    {
        var push = Substitute.For<IPushNotificationService>();
        push.SendToAllAsync(Arg.Any<PushMessageDto>()).Returns(new PushSendResultDto { Sent = 3 });
        var context = Substitute.For<ConsumeContext<MovieChanged>>();
        context.Message.Returns(new MovieChanged { MovieId = 1, Title = "Dune", Price = 1500, IsNew = isNew, IsDeleted = deleted });

        await new MovieChangedConsumer(push, NullLogger<MovieChangedConsumer>.Instance).Consume(context);

        await push.Received(expectedCalls).SendToAllAsync(Arg.Is<PushMessageDto>(m => m.Title == "New movie: Dune"));
    }
}

public class DbInitializerTests
{
    private static WebApplication BuildApp(IPublishEndpoint publish, string? password = null)
    {
        var builder = WebApplication.CreateBuilder();
        if (password != null)
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:AdminPassword"] = password });
        var dbName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<UsersDbContext>(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddIdentityCore<User>().AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<UsersDbContext>();
        builder.Services.AddSingleton(publish);
        return builder.Build();
    }

    [Fact]
    public async Task Seed_CreatesRolesAndAdminOnce_AndPublishesAdmin()
    {
        var publish = Substitute.For<IPublishEndpoint>();
        var app = BuildApp(publish);

        await app.SeedRolesAndAdminAsync();
        await app.SeedRolesAndAdminAsync();

        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = await users.FindByEmailAsync("admin@movieshop.com");
        Assert.NotNull(admin);
        Assert.True(await users.IsInRoleAsync(admin, "Admin"));
        await publish.Received(1).Publish(Arg.Is<UserChanged>(e => e.Email == "admin@movieshop.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Seed_WithInvalidConfiguredPassword_LogsAndSkipsAdmin()
    {
        var publish = Substitute.For<IPublishEndpoint>();
        var app = BuildApp(publish, password: "gyenge");

        await app.SeedRolesAndAdminAsync();

        using var scope = app.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByEmailAsync("admin@movieshop.com"));
        await publish.DidNotReceive().Publish(Arg.Any<UserChanged>(), Arg.Any<CancellationToken>());
    }
}
