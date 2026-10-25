using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.OrderService.Controllers;
using MovieShop.OrderService.DTOs;
using MovieShop.OrderService.Models;
using MovieShop.OrderService.Services;
using Stripe;

namespace MovieShop.OrderService.Tests;

internal static class Http
{
    public static T As<T>(this T controller, int userId) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"))
            }
        };
        return controller;
    }

    public static object? Field(this IActionResult result, string name)
    {
        var value = ((ObjectResult)result).Value!;
        return value.GetType().GetProperty(name)!.GetValue(value);
    }

    public static object? Prop(this object value, string name) => value.GetType().GetProperty(name)!.GetValue(value);
}

internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public static FakeHttpHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(respond(request));
    }
}

public class OrderControllerTests
{
    private readonly IOrderService _orders = Substitute.For<IOrderService>();
    private readonly IStripeService _stripe = Substitute.For<IStripeService>();
    private readonly IShoppingCartService _cart = Substitute.For<IShoppingCartService>();

    [Fact]
    public async Task CreateOrder_VerifiesPaymentBeforeCreating()
    {
        _stripe.ConfirmPaymentIntent("pi_ok").Returns(true);
        _orders.CreateOrderFromCartAsync(7, Arg.Is<OrderRequestDto>(d => d.PaymentIntentId != "pi_empty")).Returns(new OrderDto { Id = 1 });
        var c = new OrderController(_orders, _stripe).As(7);

        Assert.IsType<OkObjectResult>(await c.CreateOrder(new OrderRequestDto { PaymentIntentId = "pi_ok" }));
        Assert.IsType<OkObjectResult>(await c.CreateOrder(new OrderRequestDto()));
        Assert.IsType<BadRequestObjectResult>(await c.CreateOrder(new OrderRequestDto { PaymentIntentId = "pi_bad" }));
        Assert.IsType<BadRequestObjectResult>(await c.CreateOrder(new OrderRequestDto { PaymentIntentId = "pi_empty" }));
        _stripe.ConfirmPaymentIntent("pi_empty").Returns(true);
        Assert.IsType<BadRequestObjectResult>(await c.CreateOrder(new OrderRequestDto { PaymentIntentId = "pi_empty" }));

        c.ModelState.AddModelError("BillingAddress", "required");
        Assert.IsType<BadRequestObjectResult>(await c.CreateOrder(new OrderRequestDto()));
    }

    [Fact]
    public async Task UserAndAdminOrderQueries()
    {
        _orders.GetOrderByIdAsync(1, 7).Returns(new OrderDto { Id = 1 });
        _orders.GetOrderByIdAdminAsync(1).Returns(new OrderDto { Id = 1 });
        _orders.UpdateOrderStatusAsync(1, "Completed").Returns(true);
        var user = new OrderController(_orders, _stripe).As(7);
        var admin = new OrdersController(_orders).As(1);

        Assert.IsType<OkObjectResult>((await user.GetUserOrders()).Result);
        Assert.IsType<OkObjectResult>((await user.GetOrder(1)).Result);
        Assert.IsType<NotFoundResult>((await user.GetOrder(2)).Result);

        Assert.IsType<OkObjectResult>((await admin.GetAllOrders()).Result);
        Assert.IsType<OkObjectResult>((await admin.GetOrder(1)).Result);
        Assert.IsType<NotFoundResult>((await admin.GetOrder(2)).Result);
        Assert.IsType<OkObjectResult>((await admin.GetOrdersByStatus("Pending")).Result);
        Assert.IsType<OkObjectResult>((await admin.GetOrderStatistics()).Result);
        Assert.IsType<NoContentResult>(await admin.UpdateOrderStatus(1, new UpdateOrderStatusDto { Status = "Completed" }));
        Assert.IsType<NotFoundResult>(await admin.UpdateOrderStatus(2, new UpdateOrderStatusDto { Status = "Completed" }));
        admin.ModelState.AddModelError("Status", "required");
        Assert.IsType<BadRequestObjectResult>(await admin.UpdateOrderStatus(1, new UpdateOrderStatusDto()));
    }

    [Fact]
    public async Task Cart_RejectsAlreadyOwnedMovies()
    {
        _orders.HasUserPurchasedMovieAsync(7, 1).Returns(true);
        _cart.AddToCartAsync(7, 2, 1).Returns(true);
        _cart.UpdateCartItemQuantityAsync(7, 2, 3).Returns(true);
        _cart.RemoveFromCartAsync(7, 2).Returns(true);
        _cart.ClearCartAsync(7).Returns(true);
        var c = new ShoppingCartController(_cart, _orders).As(7);

        Assert.IsType<OkObjectResult>((await c.GetCart()).Result);
        Assert.IsType<BadRequestObjectResult>(await c.AddToCart(new AddToCartDto { MovieId = 1, Quantity = 1 }));
        Assert.IsType<OkResult>(await c.AddToCart(new AddToCartDto { MovieId = 2, Quantity = 1 }));
        Assert.IsType<BadRequestObjectResult>(await c.AddToCart(new AddToCartDto { MovieId = 3, Quantity = 1 }));
        Assert.IsType<OkResult>(await c.UpdateQuantity(new AddToCartDto { MovieId = 2, Quantity = 3 }));
        Assert.IsType<BadRequestObjectResult>(await c.UpdateQuantity(new AddToCartDto { MovieId = 3, Quantity = 3 }));
        Assert.IsType<OkResult>(await c.RemoveFromCart(2));
        Assert.IsType<NotFoundResult>(await c.RemoveFromCart(3));
        Assert.IsType<OkResult>(await c.ClearCart());
        Assert.IsType<BadRequestObjectResult>(await new ShoppingCartController(_cart, _orders).As(8).ClearCart());
    }

    [Fact]
    public async Task AdminCart_ManagesAnyUsersCart()
    {
        _cart.AddToCartAsync(5, 1, 1).Returns(true);
        _cart.UpdateCartItemQuantityAsync(5, 1, 2).Returns(true);
        _cart.RemoveFromCartAsync(5, 1).Returns(true);
        _cart.ClearCartAsync(5).Returns(true);
        var c = new AdminShoppingCartController(_cart);

        Assert.IsType<OkObjectResult>((await c.GetCartByUserId(5)).Result);
        Assert.IsType<OkResult>(await c.AddToCart(5, new AddToCartDto { MovieId = 1, Quantity = 1 }));
        Assert.IsType<BadRequestObjectResult>(await c.AddToCart(6, new AddToCartDto { MovieId = 1, Quantity = 1 }));
        Assert.IsType<OkResult>(await c.UpdateQuantity(5, new AddToCartDto { MovieId = 1, Quantity = 2 }));
        Assert.IsType<BadRequestObjectResult>(await c.UpdateQuantity(6, new AddToCartDto { MovieId = 1, Quantity = 2 }));
        Assert.IsType<OkResult>(await c.RemoveFromCart(5, 1));
        Assert.IsType<NotFoundObjectResult>(await c.RemoveFromCart(6, 1));
        Assert.IsType<OkResult>(await c.ClearCart(5));
        Assert.IsType<BadRequestObjectResult>(await c.ClearCart(6));
    }

    [Fact]
    public async Task Payment_AndRecommendations()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Stripe:PublishableKey"] = "pk_test" }).Build();
        _stripe.CreatePaymentIntent(1500).Returns("secret");
        _stripe.CreatePaymentIntent(-1).Returns<string>(_ => throw new StripeException("invalid amount"));
        _stripe.ConfirmPaymentIntent("pi").Returns(true);
        _stripe.ConfirmPaymentIntent("boom").Returns<bool>(_ => throw new Exception("down"));
        var c = new PaymentController(_stripe, config);

        var created = await c.CreatePaymentIntent(new CreatePaymentIntentRequest { Amount = 1500 });
        Assert.Equal("secret", created.Field("clientSecret"));
        Assert.Equal("pk_test", created.Field("publishableKey"));
        Assert.IsType<BadRequestObjectResult>(await c.CreatePaymentIntent(new CreatePaymentIntentRequest { Amount = -1 }));
        Assert.Equal(true, (await c.VerifyPayment(new VerifyPaymentRequest { PaymentIntentId = "pi" })).Field("success"));
        Assert.IsType<BadRequestObjectResult>(await c.VerifyPayment(new VerifyPaymentRequest { PaymentIntentId = "boom" }));
        Assert.Equal("pk_test", c.GetConfig().Field("publishableKey"));

        var recommendations = Substitute.For<IRecommendationService>();
        recommendations.GetRecommendationsAsync(7, 3).Returns(new RecommendationsResultDto());
        Assert.IsType<OkObjectResult>((await new RecommendationController(recommendations).As(7).GetRecommendations(3)).Result);
    }
}

public class InternalControllerTests : IDisposable
{
    private readonly OrdersDb _db = new();
    private readonly IRecommendationService _recommendations = Substitute.For<IRecommendationService>();

    private InternalController Create()
    {
        var context = _db.CreateContext();
        var orders = new Services.OrderService(context, new ShoppingCartService(context, NullLogger<ShoppingCartService>.Instance),
            Substitute.For<IUserServiceClient>(), Substitute.For<MassTransit.IPublishEndpoint>(), NullLogger<Services.OrderService>.Instance);
        return new InternalController(context, orders, _recommendations);
    }

    [Fact]
    public async Task AddressUsage_CountsBillingAndShipping()
    {
        await using (var ctx = _db.CreateContext())
        {
            ctx.Orders.AddRange(
                new Order { UserId = 1, BillingAddressId = 10, ShippingAddressId = 11, BillingStreet = "s", BillingCity = "c", BillingZip = "1" },
                new Order { UserId = 1, BillingAddressId = 10, BillingStreet = "s", BillingCity = "c", BillingZip = "1" });
            await ctx.SaveChangesAsync();
        }

        var usage = (List<AddressUsageDto>)((OkObjectResult)(await Create().GetAddressUsage([10, 11, 12, 10])).Result!).Value!;

        Assert.Equal(3, usage.Count);
        Assert.Equal(new AddressUsageDto(10, 2, 0), usage[0]);
        Assert.Equal(new AddressUsageDto(11, 0, 1), usage[1]);
        Assert.Empty((List<AddressUsageDto>)((OkObjectResult)(await Create().GetAddressUsage([])).Result!).Value!);
    }

    [Fact]
    public async Task UserContext_ForChatbot()
    {
        _db.SeedMovie(1, "The Matrix", 999, discountedPrice: 799);
        _db.SeedMovie(2, "Inception", 1299);
        _db.SeedOrder(7, OrderStatus.Completed, 1, 404);
        await using (var ctx = _db.CreateContext())
        {
            var cart = new ShoppingCart { UserId = 7 };
            ctx.ShoppingCarts.Add(cart);
            await ctx.SaveChangesAsync();
            ctx.ShoppingCartMovies.AddRange(
                new ShoppingCartMovie { ShoppingCartId = cart.Id, MovieId = 2, Quantity = 1, PriceAtOrder = 1000 },
                new ShoppingCartMovie { ShoppingCartId = cart.Id, MovieId = 405, Quantity = 1, PriceAtOrder = 500 });
            await ctx.SaveChangesAsync();
        }
        _recommendations.GetRecommendationContextAsync(7).Returns("ajánlások");

        var result = ((OkObjectResult)await Create().GetUserContext(7)).Value!;

        var cartItems = ((System.Collections.IEnumerable)result.Prop("cartItems")!).Cast<object>().ToList();
        Assert.Equal(1299, cartItems.Single(i => (int)i.Prop("movieId")! == 2).Prop("price"));
        Assert.Equal(500, cartItems.Single(i => (int)i.Prop("movieId")! == 405).Prop("price"));
        Assert.Equal("Unknown movie", cartItems.Single(i => (int)i.Prop("movieId")! == 405).Prop("title"));
        Assert.Equal(new List<string> { "The Matrix" }, result.Prop("purchasedTitles"));
        Assert.Equal("ajánlások", result.Prop("recommendationContext"));

        var empty = ((OkObjectResult)await Create().GetUserContext(99)).Value!;
        Assert.Empty(((System.Collections.IEnumerable)empty.Prop("cartItems")!).Cast<object>());
    }

    [Fact]
    public async Task PurchasedAndTopMovies()
    {
        _db.SeedMovie(1, "The Matrix", 999, discountedPrice: 799);
        _db.SeedMovie(2, "Inception", 1299);
        _db.SeedMovie(3, "Törölt", 100, isDeleted: true);
        _db.SeedOrder(7, OrderStatus.Completed, 1, 2);
        _db.SeedOrder(8, OrderStatus.Completed, 1, 3);
        _db.SeedOrder(9, OrderStatus.Pending, 1);

        Assert.Equal([1, 2], ((IEnumerable<int>)((OkObjectResult)await Create().GetPurchasedMovieIds(7)).Value!).Order());

        var top = ((System.Collections.IEnumerable)((OkObjectResult)await Create().GetTopMovies(5)).Value!).Cast<object>().ToList();
        Assert.Equal(2, top.Count);
        Assert.Equal("The Matrix", top[0].Prop("title"));
        Assert.Equal(3, top[0].Prop("orderCount"));
        Assert.Equal(799, top[0].Prop("price"));
    }

    public void Dispose() => _db.Dispose();
}

public class UserServiceClientTests
{
    private static UserServiceClient Create(FakeHttpHandler handler, string? authHeader = "Bearer tok3n")
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var http = new DefaultHttpContext();
        if (authHeader != null) http.Request.Headers.Authorization = authHeader;
        accessor.HttpContext.Returns(http);
        return new UserServiceClient(new HttpClient(handler) { BaseAddress = new Uri("http://user") }, accessor, NullLogger<UserServiceClient>.Instance);
    }

    [Fact]
    public async Task CreateAddress_ForwardsUserToken_AndReturnsCreated()
    {
        var handler = FakeHttpHandler.Json("""{"id":5,"street":"Fő utca 1.","city":"Budapest","zip":"1111"}""", HttpStatusCode.Created);

        var created = await Create(handler).CreateAddressAsync(new AddressDto { Street = "Fő utca 1.", City = "Budapest", Zip = "1111" });

        Assert.Equal(5, created!.Id);
        var request = handler.Requests.Single();
        Assert.Equal("Bearer tok3n", request.Headers.Authorization!.ToString());
        Assert.Equal("/api/Address", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreateAddress_FailuresReturnNull()
    {
        Assert.Null(await Create(FakeHttpHandler.Json("", HttpStatusCode.BadRequest)).CreateAddressAsync(new AddressDto()));
        Assert.Null(await Create(new FakeHttpHandler(_ => throw new HttpRequestException()), authHeader: null).CreateAddressAsync(new AddressDto()));
    }

    [Fact]
    public async Task DeleteAddress_IsBestEffort()
    {
        var ok = FakeHttpHandler.Json("", HttpStatusCode.NoContent);
        await Create(ok).DeleteAddressAsync(5);
        Assert.Equal(HttpMethod.Delete, ok.Requests.Single().Method);

        await Create(FakeHttpHandler.Json("", HttpStatusCode.NotFound)).DeleteAddressAsync(5);
        await Create(new FakeHttpHandler(_ => throw new HttpRequestException())).DeleteAddressAsync(5);
    }

    [Fact]
    public async Task NonBearerHeader_IsNotForwarded()
    {
        var handler = FakeHttpHandler.Json("""{"id":1}""");
        await Create(handler, authHeader: "Basic abc").CreateAddressAsync(new AddressDto());
        Assert.Null(handler.Requests.Single().Headers.Authorization);
    }
}

/// <summary>A Stripe SDK globális kliensét hamis HTTP-kezelőre kötjük; ezért külön, nem párhuzamos gyűjtemény.</summary>
[Collection("Stripe")]
public class StripeServiceTests : IDisposable
{
    private readonly IStripeClient _original = StripeConfiguration.StripeClient;

    private static void UseStripe(FakeHttpHandler handler)
        => StripeConfiguration.StripeClient = new StripeClient("sk_test_dummy", httpClient: new SystemNetHttpClient(new HttpClient(handler)));

    [Fact]
    public async Task CreatePaymentIntent_SendsAmountInMinorUnits()
    {
        var handler = FakeHttpHandler.Json("""{"id":"pi_1","object":"payment_intent","client_secret":"pi_1_secret","status":"requires_payment_method"}""");
        UseStripe(handler);

        var secret = await new StripeService(NullLogger<StripeService>.Instance).CreatePaymentIntent(15.5m);

        Assert.Equal("pi_1_secret", secret);
        var body = await handler.Requests.Single().Content!.ReadAsStringAsync();
        Assert.Contains("amount=1550", body);
        Assert.Contains("currency=huf", body);
    }

    [Fact]
    public async Task ConfirmAndGet_ReadStatus_AndErrorsAreFalse()
    {
        UseStripe(FakeHttpHandler.Json("""{"id":"pi_1","object":"payment_intent","status":"succeeded"}"""));
        var service = new StripeService(NullLogger<StripeService>.Instance);
        Assert.True(await service.ConfirmPaymentIntent("pi_1"));
        Assert.Equal("succeeded", (await service.GetPaymentIntent("pi_1")).Status);

        UseStripe(FakeHttpHandler.Json("""{"id":"pi_2","object":"payment_intent","status":"processing"}"""));
        Assert.False(await service.ConfirmPaymentIntent("pi_2"));

        UseStripe(FakeHttpHandler.Json("""{"error":{"type":"invalid_request_error","message":"No such payment_intent"}}""", HttpStatusCode.NotFound));
        Assert.False(await service.ConfirmPaymentIntent("pi_x"));
    }

    public void Dispose() => StripeConfiguration.StripeClient = _original;
}
