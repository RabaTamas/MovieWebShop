using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.ChatService.Controllers;
using MovieShop.ChatService.Models;
using MovieShop.ChatService.Services;

namespace MovieShop.ChatService.Tests;

internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

    public static FakeHttpHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add((request, request.Content == null ? null : await request.Content.ReadAsStringAsync(ct)));
        return respond(request);
    }
}

public class ChatControllerTests
{
    private readonly IChatService _chat = Substitute.For<IChatService>();
    private readonly IAgentService _agent = Substitute.For<IAgentService>();
    private readonly IGroqClient _groq = Substitute.For<IGroqClient>();

    public ChatControllerTests()
    {
        _groq.IsConfigured.Returns(true);
        _chat.GetContextualAnswer(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>()).Returns("AI-válasz");
        _agent.ProcessAsync(Arg.Any<string>(), 7, Arg.Any<string>())
            .Returns(new AgentChatResponse { Answer = "Agent-válasz", Source = "agent", Action = new AgentAction { Type = "navigate" } });
    }

    private ChatController Create(bool loggedIn = true)
    {
        var c = new ChatController(_chat, _agent, _groq, NullLogger<ChatController>.Instance);
        var identity = loggedIn ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7")], "Test") : new ClaimsIdentity();
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return c;
    }

    private static string? Source(IActionResult result)
    {
        var value = ((OkObjectResult)result).Value!;
        return (string?)value.GetType().GetProperty("source")!.GetValue(value);
    }

    [Fact]
    public async Task EmptyQuestion_Is400()
        => Assert.IsType<BadRequestObjectResult>(await Create().AskQuestion(new ChatRequest { Question = " " }));

    [Theory]
    [InlineData("What payment method do you accept?")]
    [InlineData("how to pay")]
    [InlineData("Do I get instant access?")]
    [InlineData("how to watch")]
    [InlineData("can download?")]
    [InlineData("refund please")]
    [InlineData("contact")]
    public async Task Faq_IsAnsweredImmediately(string question)
        => Assert.Equal("FAQ", Source(await Create().AskQuestion(new ChatRequest { Question = question })));

    [Theory]
    [InlineData("add The Matrix to my cart")]
    [InlineData("remove Inception from my cart")]
    [InlineData("remove Inception")]
    [InlineData("I want to buy Dune")]
    [InlineData("go to auctions")]
    [InlineData("take me to my orders")]
    [InlineData("open cart")]
    [InlineData("proceed to checkout")]
    [InlineData("show my profile")]
    [InlineData("change my name to Neo")]
    [InlineData("search for Dune")]
    [InlineData("do you have Dune")]
    [InlineData("I want to watch The Matrix")]
    [InlineData("start a watch party")]
    [InlineData("set my billing address")]
    [InlineData("update my address please")]
    public async Task ActionQuestions_GoToAgent(string question)
        => Assert.Equal("agent", Source(await Create().AskQuestion(new ChatRequest { Question = question, SessionId = "s" })));

    [Theory]
    [InlineData("tell me about The Matrix")]
    [InlineData("watch")]
    public async Task InformationalQuestions_GoToContextualAnswer(string question)
        => Assert.Equal("AI", Source(await Create().AskQuestion(new ChatRequest { Question = question })));

    [Fact]
    public async Task AgentIsSkipped_ForAnonymousOrUnconfigured_AndFailsOverToAi()
    {
        Assert.Equal("AI", Source(await Create(loggedIn: false).AskQuestion(new ChatRequest { Question = "go to cart" })));

        _agent.ProcessAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns<AgentChatResponse>(_ => throw new Exception("Groq down"));
        Assert.Equal("AI", Source(await Create().AskQuestion(new ChatRequest { Question = "go to cart" })));

        _groq.IsConfigured.Returns(false);
        Assert.Equal("AI", Source(await Create().AskQuestion(new ChatRequest { Question = "go to cart" })));
    }
}

public class GroqClientTests
{
    private static GroqClient Create(FakeHttpHandler handler, string? key = "gsk_test")
        => new(new HttpClient(handler),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Groq:ApiKey"] = key }).Build(),
            NullLogger<GroqClient>.Instance);

    [Fact]
    public async Task Send_PostsSnakeCaseJsonWithBearerKey()
    {
        var handler = FakeHttpHandler.Json("""{"choices":[]}""");

        var result = await Create(handler).SendAsync(new { maxTokens = 10 });

        Assert.NotNull(result["choices"]);
        var (request, body) = handler.Requests.Single();
        Assert.Equal("Bearer gsk_test", request.Headers.Authorization!.ToString());
        Assert.Contains("\"max_tokens\":10", body);
    }

    [Fact]
    public async Task Errors_AreReportedAsGroqApiException()
    {
        Assert.False(Create(FakeHttpHandler.Json("{}"), key: null).IsConfigured);
        await Assert.ThrowsAsync<GroqApiException>(() => Create(FakeHttpHandler.Json("{}"), key: null).SendAsync(new { }));

        var ex = await Assert.ThrowsAsync<GroqApiException>(() =>
            Create(FakeHttpHandler.Json("""{"error":{"message":"rate limit"}}""", HttpStatusCode.TooManyRequests)).SendAsync(new { }));
        Assert.Contains("rate limit", ex.ResponseJson);
        Assert.Null(ex.GetFailedGeneration());

        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(FakeHttpHandler.Json("null")).SendAsync(new { }));
    }
}

public class ServiceClientsTests
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = [];
    private readonly List<(HttpRequestMessage Request, string? Body)> _requests = [];

    private ServiceClients Create(string? auth = "Bearer user-token", bool configured = true)
    {
        var handler = new FakeHttpHandler(req =>
        {
            var key = $"{req.Method} {req.RequestUri!.Host}{req.RequestUri.PathAndQuery}";
            return _routes.TryGetValue(key, out var respond)
                ? respond(req)
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("") };
        });
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(new RecordingHandler(handler, _requests), disposeHandler: false));

        var accessor = Substitute.For<IHttpContextAccessor>();
        var http = new DefaultHttpContext();
        if (auth != null) http.Request.Headers.Authorization = auth;
        accessor.HttpContext.Returns(http);

        var config = new ConfigurationBuilder().AddInMemoryCollection(configured
            ? new Dictionary<string, string?>
            {
                ["Services:CatalogService"] = "http://catalog",
                ["Services:OrderService"] = "http://order",
                ["Services:UserService"] = "http://user"
            }
            : []).Build();

        return new ServiceClients(factory, accessor, config, NullLogger<ServiceClients>.Instance);
    }

    private void Route(string key, string json, HttpStatusCode status = HttpStatusCode.OK)
        => _routes[key] = _ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Reads_DeserializeAndForwardUserToken()
    {
        Route("GET catalog/api/internal/catalog/movies", """[{"id":1,"title":"The Matrix","price":999,"categories":["Action"],"reviews":[]}]""");
        Route("GET catalog/api/Category", """[{"id":1,"name":"Action"}]""");
        Route("GET order/api/internal/orders/context/7", """{"cartItems":[{"movieId":1,"title":"M","quantity":1,"price":799}],"purchasedMovieIds":[2]}""");
        Route("GET order/api/internal/orders/top-movies?count=3", """[{"movieId":1,"title":"M","orderCount":5,"price":799}]""");
        Route("GET user/api/internal/users/lookup?userIds=7", """[{"userId":7,"userName":"neo","email":"neo@zion.io"}]""");
        Route("GET user/api/Address", """[{"id":3,"street":"Fő u.","city":"Bp","zip":"1111"}]""");
        var c = Create();

        Assert.Equal("The Matrix", (await c.GetCatalogAsync()).Single().Title);
        Assert.Equal("Action", (await c.GetCategoriesAsync()).Single().Name);
        Assert.Equal([2], (await c.GetUserContextAsync(7))!.PurchasedMovieIds);
        Assert.Equal(5, (await c.GetTopMoviesAsync(3)).Single().OrderCount);
        Assert.Equal("neo", (await c.GetUserInfoAsync(7))!.UserName);
        Assert.Equal(3, (await c.GetAddressesAsync()).Single().Id);
        Assert.All(_requests, r => Assert.Equal("Bearer user-token", r.Request.Headers.Authorization!.ToString()));
    }

    [Fact]
    public async Task Reads_DegradeToEmptyOnErrors()
    {
        var c = Create(auth: null);

        Assert.Empty(await c.GetCatalogAsync());
        Assert.Empty(await c.GetCategoriesAsync());
        Assert.Null(await c.GetUserContextAsync(7));
        Assert.Empty(await c.GetTopMoviesAsync());
        Assert.Null(await c.GetUserInfoAsync(7));
        Assert.Empty(await c.GetAddressesAsync());

        var unconfigured = Create(configured: false);
        Assert.Empty(await unconfigured.GetCatalogAsync());
        Assert.False(await unconfigured.AddToCartAsync(1));
    }

    [Fact]
    public async Task Writes_SendPayloadsAndReportSuccess()
    {
        Route("POST order/api/ShoppingCart/add", "");
        Route("DELETE order/api/ShoppingCart/remove/1", "");
        Route("PUT user/api/User/display-name", """{"message":"ok"}""");
        Route("POST user/api/Address", "{}");
        Route("PUT user/api/Address/3", "{}");
        var c = Create();

        Assert.True(await c.AddToCartAsync(1));
        Assert.Contains("\"movieId\":1", _requests.Last().Body);
        Assert.True(await c.RemoveFromCartAsync(1));
        Assert.Equal((true, (string?)null), await c.UpdateDisplayNameAsync("Neo"));
        Assert.True(await c.CreateAddressAsync("Fő u.", "Bp", "1111"));
        Assert.True(await c.UpdateAddressAsync(3, "Fő u.", "Bp", "1111"));
        Assert.False(await c.RemoveFromCartAsync(99));
    }

    [Theory]
    [InlineData("""{"message":"User not found"}""", "User not found")]
    [InlineData("""{"title":"Bad"}""", """{"title":"Bad"}""")]
    [InlineData("nem json", "nem json")]
    [InlineData("", "")]
    public async Task DisplayNameErrors_ExtractServerMessage(string body, string expected)
    {
        Route("PUT user/api/User/display-name", body, HttpStatusCode.BadRequest);

        Assert.Equal((false, expected), await Create().UpdateDisplayNameAsync("x"));
    }

    private sealed class RecordingHandler(HttpMessageHandler inner, List<(HttpRequestMessage, string?)> log) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            log.Add((request, request.Content == null ? null : await request.Content.ReadAsStringAsync(ct)));
            return await base.SendAsync(request, ct);
        }
    }
}

public class ConversationServiceTests
{
    [Fact]
    public void History_IsCappedAndClearable()
    {
        var service = new ConversationService();
        for (var i = 0; i < 25; i++) service.AddMessage("s1", "user", $"m{i}");

        Assert.Equal(10, service.GetHistory("s1").Count);
        Assert.Equal("m24", service.GetHistory("s1", 50).Last().Content);
        Assert.Equal(20, service.GetHistory("s1", 50).Count);
        Assert.Empty(service.GetHistory("ismeretlen"));

        service.ClearHistory("s1");
        Assert.Empty(service.GetHistory("s1"));
    }

    [Fact]
    public void Cleanup_RemovesOnlyIdleSessions()
    {
        var service = new ConversationService();
        service.AddMessage("friss", "user", "x");

        service.CleanupOldSessions(maxAgeMinutes: 30);
        Assert.Single(service.GetHistory("friss"));

        service.CleanupOldSessions(maxAgeMinutes: -1);
        Assert.Empty(service.GetHistory("friss"));
    }

    [Fact]
    public async Task CleanupService_StopsGracefully()
    {
        var worker = new ConversationCleanupService(new ConversationService(), NullLogger<ConversationCleanupService>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.ExecuteTask!.IsCompleted);
    }

    [Fact]
    public void FailedGeneration_IsExtractedOnlyForToolUseErrors()
    {
        var ex = new GroqApiException("""{"error":{"code":"tool_use_failed","failed_generation":"<function=x{}"}}""");
        Assert.Equal("<function=x{}", ex.GetFailedGeneration());
        Assert.Null(new GroqApiException("nem json").GetFailedGeneration());
    }
}
