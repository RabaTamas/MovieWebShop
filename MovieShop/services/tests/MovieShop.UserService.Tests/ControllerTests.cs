using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.Contracts.Events;
using MovieShop.UserService.Controllers;
using MovieShop.UserService.DTOs;
using MovieShop.UserService.Services;

namespace MovieShop.UserService.Tests;

public class AuthControllerTests
{
    private readonly IAuthService _auth = Substitute.For<IAuthService>();

    private AuthController Create(int userId = 7) => new AuthController(_auth).As(userId);

    [Fact]
    public async Task PublicEndpoints_MapApplicationErrorsTo400()
    {
        _auth.RegisterAsync(Arg.Is<UserRegisterDto>(d => d.Email == "ok")).Returns(new AuthResultDto { Token = "t" });
        _auth.RegisterAsync(Arg.Is<UserRegisterDto>(d => d.Email == "bad")).Returns<AuthResultDto>(_ => throw new ApplicationException("Email is already registered"));
        _auth.LoginAsync(Arg.Is<UserLoginDto>(d => d.Email == "ok")).Returns(new AuthResultDto());
        _auth.LoginAsync(Arg.Is<UserLoginDto>(d => d.Email == "bad")).Returns<AuthResultDto>(_ => throw new ApplicationException("Invalid email or password"));
        _auth.GoogleLoginAsync("ok").Returns(new AuthResultDto());
        _auth.GoogleLoginAsync("bad").Returns<AuthResultDto>(_ => throw new ApplicationException("Google login failed"));
        _auth.TwoFactorLoginAsync("1", "ok").Returns(new AuthResultDto());
        _auth.TwoFactorLoginAsync("1", "bad").Returns<AuthResultDto>(_ => throw new ApplicationException("Invalid authentication code"));
        var c = Create();

        Assert.IsType<OkObjectResult>((await c.Register(new UserRegisterDto { Email = "ok" })).Result);
        Assert.IsType<BadRequestObjectResult>((await c.Register(new UserRegisterDto { Email = "bad" })).Result);
        Assert.IsType<OkObjectResult>((await c.Login(new UserLoginDto { Email = "ok" })).Result);
        Assert.IsType<BadRequestObjectResult>((await c.Login(new UserLoginDto { Email = "bad" })).Result);
        Assert.IsType<OkObjectResult>((await c.GoogleLogin(new GoogleLoginDto { IdToken = "ok" })).Result);
        Assert.IsType<BadRequestObjectResult>((await c.GoogleLogin(new GoogleLoginDto { IdToken = "bad" })).Result);
        Assert.IsType<OkObjectResult>((await c.TwoFactorLogin(new TwoFactorLoginDto { TwoFactorUserId = "1", Code = "ok" })).Result);
        Assert.IsType<BadRequestObjectResult>((await c.TwoFactorLogin(new TwoFactorLoginDto { TwoFactorUserId = "1", Code = "bad" })).Result);
    }

    [Fact]
    public async Task RoleManagement()
    {
        _auth.AssignRoleAsync(1, "Admin").Returns(true);
        _auth.AssignRoleAsync(1, "User").Returns(true);
        var c = Create();

        Assert.IsType<OkObjectResult>(await c.MakeAdmin(1));
        Assert.IsType<NotFoundObjectResult>(await c.MakeAdmin(2));
        Assert.IsType<OkObjectResult>(await c.RemoveAdmin(1));
        Assert.IsType<NotFoundObjectResult>(await c.RemoveAdmin(2));
    }

    [Fact]
    public async Task TwoFactorEndpoints_UseCurrentUser()
    {
        _auth.GetTwoFactorSetupAsync(7).Returns(new TwoFactorSetupDto { SharedKey = "K" });
        _auth.EnableTwoFactorAsync(7, "123456").Returns(["r1", "r2"]);
        _auth.EnableTwoFactorAsync(7, "000000").Returns<IEnumerable<string>>(_ => throw new ApplicationException("Invalid verification code"));
        _auth.DisableTwoFactorAsync(7, "000000").Returns(_ => throw new ApplicationException("Invalid verification code"));
        _auth.GetTwoFactorStatusAsync(7).Returns(true);

        Assert.IsType<OkObjectResult>((await Create().GetTwoFactorSetup()).Result);
        _auth.GetTwoFactorSetupAsync(8).Returns<TwoFactorSetupDto>(_ => throw new ApplicationException("User not found"));
        Assert.IsType<BadRequestObjectResult>((await Create(8).GetTwoFactorSetup()).Result);

        Assert.Equal(new[] { "r1", "r2" }, (IEnumerable<string>)(await Create().EnableTwoFactor(new EnableTwoFactorDto { Code = "123456" })).Field("recoveryCodes")!);
        Assert.IsType<BadRequestObjectResult>(await Create().EnableTwoFactor(new EnableTwoFactorDto { Code = "000000" }));
        Assert.IsType<OkObjectResult>(await Create().DisableTwoFactor(new EnableTwoFactorDto { Code = "123456" }));
        Assert.IsType<BadRequestObjectResult>(await Create().DisableTwoFactor(new EnableTwoFactorDto { Code = "000000" }));

        var status = (TwoFactorStatusDto)((OkObjectResult)(await Create().GetTwoFactorStatus()).Result!).Value!;
        Assert.True(status.IsEnabled);
    }
}

public class UserControllerTests
{
    private readonly IProfileService _profile = Substitute.For<IProfileService>();

    private UserController Create(int userId = 7, string role = "User") => new UserController(_profile).As(userId, role);

    [Fact]
    public async Task Profile_EmailAndPassword()
    {
        _profile.GetProfileAsync(7).Returns(new UserProfileDto { Email = "neo@zion.io" });
        _profile.UpdateEmailAsync(7, Arg.Any<UpdateEmailDto>()).Returns(true);
        _profile.ChangePasswordAsync(7, Arg.Any<ChangePasswordDto>()).Returns(true);

        Assert.IsType<OkObjectResult>((await Create().GetProfile()).Result);
        Assert.IsType<NotFoundResult>((await Create(8).GetProfile()).Result);
        Assert.IsType<NoContentResult>(await Create().UpdateEmail(new UpdateEmailDto()));
        Assert.IsType<BadRequestObjectResult>(await Create(8).UpdateEmail(new UpdateEmailDto()));
        Assert.IsType<NoContentResult>(await Create().ChangePassword(new ChangePasswordDto()));
        Assert.IsType<BadRequestObjectResult>(await Create(8).ChangePassword(new ChangePasswordDto()));
    }

    [Fact]
    public async Task MissingUserClaim_IsUnauthorized()
    {
        var c = new UserController(_profile).Anonymous();

        Assert.IsType<UnauthorizedObjectResult>((await c.GetProfile()).Result);
        Assert.IsType<UnauthorizedObjectResult>(await c.UpdateEmail(new UpdateEmailDto()));
        Assert.IsType<UnauthorizedObjectResult>(await c.ChangePassword(new ChangePasswordDto()));
        Assert.IsType<UnauthorizedObjectResult>(await c.UpdateDisplayName(new UpdateDisplayNameDto()));
    }

    [Fact]
    public async Task DisplayName_ValidatesAndMapsErrors()
    {
        _profile.UpdateDisplayNameAsync(7, "TheOne").Returns((true, (string?)null));
        _profile.UpdateDisplayNameAsync(7, "bad name").Returns((false, (string?)"invalid"));
        _profile.UpdateDisplayNameAsync(8, "X").Returns((false, (string?)"User not found"));

        Assert.IsType<OkObjectResult>(await Create().UpdateDisplayName(new UpdateDisplayNameDto { NewName = "TheOne" }));
        Assert.IsType<BadRequestObjectResult>(await Create().UpdateDisplayName(new UpdateDisplayNameDto { NewName = "bad name" }));
        Assert.IsType<NotFoundObjectResult>(await Create(8).UpdateDisplayName(new UpdateDisplayNameDto { NewName = "X" }));
        Assert.IsType<BadRequestObjectResult>(await Create().UpdateDisplayName(new UpdateDisplayNameDto { NewName = " " }));
    }

    [Fact]
    public async Task AdminUserManagement()
    {
        _profile.GetAllUsersAsync().Returns([new UserDto { Id = 1 }]);
        _profile.DeleteUserAsync(2).Returns(true);
        var admin = Create(1, "Admin");

        Assert.IsType<OkObjectResult>((await admin.GetAllUsers()).Result);
        Assert.IsType<NoContentResult>(await admin.DeleteUser(2));
        Assert.IsType<NotFoundObjectResult>(await admin.DeleteUser(3));
        Assert.IsType<BadRequestObjectResult>(await admin.DeleteUser(1));
    }
}

public class AddressControllerTests
{
    private readonly IAddressService _service = Substitute.For<IAddressService>();
    private readonly IAdminAddressService _admin = Substitute.For<IAdminAddressService>();

    [Fact]
    public async Task UserAddresses()
    {
        _service.GetUserAddressesAsync(7).Returns([]);
        _service.GetAddressByIdAsync(1, 7).Returns(new AddressDto { Id = 1 });
        _service.GetAddressByIdAsync(2, 7).Returns<AddressDto>(_ => throw new KeyNotFoundException("nincs"));
        _service.CreateAddressAsync(Arg.Any<AddressDto>(), 7).Returns(new AddressDto { Id = 5 });
        _service.UpdateAddressAsync(1, Arg.Any<AddressDto>(), 7).Returns(new AddressDto { Id = 1 });
        _service.UpdateAddressAsync(2, Arg.Any<AddressDto>(), 7).Returns<AddressDto>(_ => throw new KeyNotFoundException("nincs"));
        _service.DeleteAddressAsync(1, 7).Returns(true);
        _service.DeleteAddressAsync(3, 7).Returns<bool>(_ => throw new InvalidOperationException("használt"));
        var c = new AddressController(_service).As(7);

        Assert.IsType<OkObjectResult>((await c.GetUserAddresses()).Result);
        Assert.IsType<OkObjectResult>((await c.GetAddress(1)).Result);
        Assert.IsType<NotFoundObjectResult>((await c.GetAddress(2)).Result);
        Assert.IsType<CreatedAtActionResult>((await c.CreateAddress(new AddressDto())).Result);
        Assert.IsType<OkObjectResult>((await c.UpdateAddress(1, new AddressDto())).Result);
        Assert.IsType<NotFoundObjectResult>((await c.UpdateAddress(2, new AddressDto())).Result);
        Assert.IsType<NoContentResult>(await c.DeleteAddress(1));
        Assert.IsType<NotFoundResult>(await c.DeleteAddress(2));
        Assert.IsType<BadRequestObjectResult>(await c.DeleteAddress(3));
    }

    [Fact]
    public async Task AdminAddresses()
    {
        _admin.GetAllAddressesAsync().Returns([]);
        _admin.GetAddressByIdAsync(1).Returns(new AdminAddressDto());
        _admin.GetAddressByIdAsync(2).Returns<AdminAddressDto>(_ => throw new KeyNotFoundException("nincs"));
        _admin.UpdateAddressAsync(1, Arg.Any<AddressDto>()).Returns(new AddressDto());
        _admin.UpdateAddressAsync(2, Arg.Any<AddressDto>()).Returns<AddressDto>(_ => throw new KeyNotFoundException("nincs"));
        _admin.DeleteAddressAsync(1).Returns(true);
        _admin.DeleteAddressAsync(3).Returns<bool>(_ => throw new InvalidOperationException("használt"));
        var c = new AdminAddressController(_admin);

        Assert.IsType<OkObjectResult>((await c.GetAllAddresses()).Result);
        Assert.IsType<OkObjectResult>((await c.GetAddress(1)).Result);
        Assert.IsType<NotFoundObjectResult>((await c.GetAddress(2)).Result);
        Assert.IsType<OkObjectResult>((await c.UpdateAddress(1, new AddressDto())).Result);
        Assert.IsType<NotFoundObjectResult>((await c.UpdateAddress(2, new AddressDto())).Result);
        Assert.IsType<NoContentResult>(await c.DeleteAddress(1));
        Assert.IsType<NotFoundResult>(await c.DeleteAddress(2));
        Assert.IsType<BadRequestObjectResult>(await c.DeleteAddress(3));
    }
}

public class PushControllerTests
{
    private readonly IPushNotificationService _push = Substitute.For<IPushNotificationService>();

    [Fact]
    public async Task NotConfigured_Is503()
    {
        _push.IsConfigured.Returns(false);
        var c = new PushController(_push).As(7);

        Assert.Equal(503, c.GetVapidPublicKey().Status());
        Assert.Equal(503, (await c.Subscribe(new PushSubscriptionDto())).Status());
        Assert.Equal(503, ((ObjectResult)(await c.SendTest()).Result!).StatusCode);
    }

    [Fact]
    public async Task Configured_SubscribesWithUserAgent_AndSendsTest()
    {
        _push.IsConfigured.Returns(true);
        _push.PublicKey.Returns("BPub");
        _push.UnsubscribeAsync(7, "e").Returns(true);
        _push.SendToUserAsync(7, Arg.Any<PushMessageDto>()).Returns(new PushSendResultDto { Sent = 1 });
        var c = new PushController(_push).As(7, userAgent: "Firefox");

        Assert.Equal("BPub", c.GetVapidPublicKey().Field("publicKey"));
        Assert.Equal(true, (await c.Subscribe(new PushSubscriptionDto { Endpoint = "e" })).Field("subscribed"));
        await _push.Received(1).SubscribeAsync(7, Arg.Any<PushSubscriptionDto>(), "Firefox");
        Assert.Equal(true, (await c.Unsubscribe(new PushUnsubscribeDto { Endpoint = "e" })).Field("removed"));
        Assert.Equal(1, ((PushSendResultDto)((OkObjectResult)(await c.SendTest()).Result!).Value!).Sent);
    }
}

public class InternalControllerTests : IDisposable
{
    private readonly IdentityHost _host = new();

    [Fact]
    public async Task Republish_AndLookup()
    {
        var neo = await _host.CreateUserAsync("neo", "neo@zion.io");
        await _host.CreateUserAsync("trinity", "trinity@zion.io");
        var publish = Substitute.For<IPublishEndpoint>();
        var c = new InternalController(_host.NewDb(), publish, NullLogger<InternalController>.Instance);

        Assert.Equal(2, (await c.RepublishAll()).Field("republished"));
        await publish.Received(2).Publish(Arg.Any<UserChanged>(), Arg.Any<CancellationToken>());

        var found = ((System.Collections.IEnumerable)((OkObjectResult)await c.Lookup([neo.Id])).Value!).Cast<object>().ToList();
        Assert.Single(found);
        Assert.Empty(((System.Collections.IEnumerable)((OkObjectResult)await c.Lookup([])).Value!).Cast<object>());
    }

    public void Dispose() => _host.Dispose();
}
