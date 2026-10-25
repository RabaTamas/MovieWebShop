using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.ChatService.Models;
using MovieShop.ChatService.Services;

namespace MovieShop.ChatService.Tests;

internal static class Groq
{
    public static JsonNode Answer(string text, string finish = "stop")
        => JsonNode.Parse(JsonSerializer.Serialize(new
        {
            choices = new[] { new { finish_reason = finish, message = new { role = "assistant", content = text } } }
        }))!;

    public static JsonNode ToolCall(string name, object args, string? content = null)
        => JsonNode.Parse(JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    finish_reason = "tool_calls",
                    message = new
                    {
                        role = "assistant",
                        content,
                        tool_calls = new[]
                        {
                            new { id = "call_1", type = "function", function = new { name, arguments = JsonSerializer.Serialize(args) } }
                        }
                    }
                }
            }
        }))!;

    /// <summary>A Groq elküldött kérése JSON-ként (a felhasználói prompt ellenőrzéséhez).</summary>
    public static string LastUserPrompt(IGroqClient groq)
    {
        var body = groq.ReceivedCalls().Last().GetArguments()[0]!;
        var json = JsonNode.Parse(JsonSerializer.Serialize(body))!;
        return json["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();
    }
}

public class ChatServiceTests
{
    private readonly IServiceClients _services = Substitute.For<IServiceClients>();
    private readonly IGroqClient _groq = Substitute.For<IGroqClient>();
    private readonly ConversationService _conversations = new();

    private static readonly List<CatalogMovie> Catalog =
    [
        new() { Id = 1, Title = "The Matrix", Description = "Hacker", Price = 999, DiscountedPrice = 799, Categories = ["Action"],
                Reviews = [new CatalogReview { UserName = "neo", Content = "Kiváló" }, new CatalogReview { UserName = "", Content = "Jó" }] },
        new() { Id = 2, Title = "Inception", Description = "Álmok", Price = 1299, Categories = ["Thriller"] },
        new() { Id = 3, Title = "Notebook", Description = "Romantikus", Price = 600, Categories = ["Drama"] }
    ];

    public ChatServiceTests()
    {
        _services.GetCatalogAsync().Returns(Catalog);
        _groq.SendAsync(Arg.Any<object>()).Returns(Groq.Answer(" Válasz "));
    }

    private ChatService.Services.ChatService Create() =>
        new(_services, _conversations, _groq, new ConfigurationBuilder().Build(), NullLogger<ChatService.Services.ChatService>.Instance);

    [Fact]
    public async Task MovieQuestion_SendsVerifiedMovieDataAndReviews_AndRemembersConversation()
    {
        var answer = await Create().GetContextualAnswer("Tell me about The Matrix", "s1");

        Assert.Equal("Válasz", answer);
        var prompt = Groq.LastUserPrompt(_groq);
        Assert.Contains("Movie: The Matrix", prompt);
        Assert.Contains("Price: 799 Ft", prompt);
        Assert.Contains("Review by neo", prompt);
        Assert.Contains("Review by Anonymous", prompt);
        Assert.Equal(2, _conversations.GetHistory("s1").Count);
    }

    [Fact]
    public async Task MovieWithoutReviews_SaysSo()
    {
        await Create().GetContextualAnswer("what is inception about", "s1");

        Assert.Contains("No reviews yet", Groq.LastUserPrompt(_groq));
    }

    [Fact]
    public async Task UnknownMovie_IsAnsweredWithoutCallingTheModel()
    {
        var answer = await Create().GetContextualAnswer("Do you have Titanic?", "s1");

        Assert.Contains("we don't have 'titanic'", answer, StringComparison.OrdinalIgnoreCase);
        await _groq.DidNotReceive().SendAsync(Arg.Any<object>());
    }

    [Fact]
    public async Task LoggedInUser_GetsPersonalContext()
    {
        _services.GetUserInfoAsync(7).Returns(new UserInfo { UserName = "neo", Email = "neo@zion.io" });
        _services.GetUserContextAsync(7).Returns(new UserOrderContext
        {
            CartItems = [new CartItem { Title = "Inception", Quantity = 1, Price = 1299 }],
            RecentOrders = [new RecentOrder { Id = 5, OrderDate = DateTime.UtcNow, Status = "Completed", TotalPrice = 799, Movies = ["The Matrix"] }],
            PurchasedTitles = ["The Matrix"]
        });

        await Create().GetContextualAnswer("what's in my order history", "s1", "7");

        var prompt = Groq.LastUserPrompt(_groq);
        Assert.Contains("User: neo (neo@zion.io)", prompt);
        Assert.Contains("Cart Total: 1299 Ft", prompt);
        Assert.Contains("Order #5", prompt);
        Assert.Contains("ALL PURCHASED MOVIES (1 unique titles)", prompt);
    }

    [Fact]
    public async Task EmptyCartAndNoOrders_AreStatedExplicitly_AndMissingOrderServiceOmitsSection()
    {
        _services.GetUserContextAsync(7).Returns(new UserOrderContext());
        await Create().GetContextualAnswer("hello", "s1", "7");
        var prompt = Groq.LastUserPrompt(_groq);
        Assert.Contains("SHOPPING CART: Empty", prompt);
        Assert.Contains("RECENT ORDERS: No orders yet", prompt);

        _services.GetUserContextAsync(8).Returns((UserOrderContext?)null);
        await Create().GetContextualAnswer("hello", "s2", "8");
        Assert.DoesNotContain("SHOPPING CART", Groq.LastUserPrompt(_groq));
    }

    [Fact]
    public async Task Recommendations_ExcludeOwnedMovies()
    {
        _services.GetUserContextAsync(7).Returns(new UserOrderContext { PurchasedMovieIds = [1], RecommendationContext = "AJÁNLÁS" });

        await Create().GetContextualAnswer("can you recommend something", "s1", "7");

        var prompt = Groq.LastUserPrompt(_groq);
        Assert.Contains("AJÁNLÁS", prompt);
        Assert.Contains("- Inception (1299 Ft)", prompt);
        Assert.DoesNotContain("- The Matrix", prompt);
    }

    [Fact]
    public async Task PopularMovies_UseSalesData_OrFallbackText()
    {
        _services.GetTopMoviesAsync(5).Returns([new TopMovie { Title = "The Matrix", OrderCount = 3, Price = 799 }], new List<TopMovie>());

        await Create().GetContextualAnswer("what is popular now", "s1");
        Assert.Contains("- The Matrix (3 orders, 799 Ft)", Groq.LastUserPrompt(_groq));

        await Create().GetContextualAnswer("show trending", "s2");
        Assert.Contains("no sales data yet", Groq.LastUserPrompt(_groq));
    }

    [Fact]
    public async Task Categories_ListMatchingMovies_OrAllCategories()
    {
        _services.GetCategoriesAsync().Returns([new CategoryItem { Name = "Action" }, new CategoryItem { Name = "Drama" }, new CategoryItem { Name = "Comedy" }]);

        await Create().GetContextualAnswer("show me action films", "s1");
        Assert.Contains("Featured movies:\n- The Matrix (799 Ft)", Groq.LastUserPrompt(_groq));

        await Create().GetContextualAnswer("any comedy", "s2");
        Assert.Contains("Available movies: 0", Groq.LastUserPrompt(_groq));

        await Create().GetContextualAnswer("which genre do you offer", "s3");
        Assert.Contains("Available categories: Action, Drama, Comedy", Groq.LastUserPrompt(_groq));
    }

    [Fact]
    public async Task FollowUpQuestion_ResolvesReferenceFromHistory()
    {
        _conversations.AddMessage("s1", "assistant", "I recommend the movie \"Inception\".");

        await Create().GetContextualAnswer("how much is it", "s1");

        Assert.Contains("Movie: Inception", Groq.LastUserPrompt(_groq));
    }

    [Fact]
    public async Task ModelErrors_AreTranslatedToFriendlyMessages()
    {
        _groq.SendAsync(Arg.Any<object>()).Returns<JsonNode>(_ => throw new GroqApiException("{\"error\":{}}"));
        Assert.Contains("having trouble", await Create().GetContextualAnswer("hello", "s1"));

        _groq.SendAsync(Arg.Any<object>()).Returns<JsonNode>(_ => throw new HttpRequestException());
        Assert.StartsWith("Technical error", await Create().GetContextualAnswer("hello", "s2"));

        _groq.SendAsync(Arg.Any<object>()).Returns(JsonNode.Parse("""{"choices":[{"message":{}}]}""")!);
        Assert.StartsWith("I couldn't generate", await Create().GetContextualAnswer("hello", "s3"));
    }

    [Fact]
    public async Task UserContextFailure_IsTolerated()
    {
        _services.GetUserInfoAsync(7).Returns<UserInfo?>(_ => throw new Exception("User Service down"));
        _services.GetUserContextAsync(7).Returns(new UserOrderContext());

        Assert.Equal("Válasz", await Create().GetContextualAnswer("hello", "s1", "7"));
    }
}

public class AgentServiceTests
{
    private readonly IServiceClients _services = Substitute.For<IServiceClients>();
    private readonly IGroqClient _groq = Substitute.For<IGroqClient>();

    public AgentServiceTests()
    {
        _groq.IsConfigured.Returns(true);
        _services.GetCatalogAsync().Returns(
        [
            new CatalogMovie { Id = 1, Title = "The Matrix", Price = 999, DiscountedPrice = 799, Categories = ["Action"] },
            new CatalogMovie { Id = 2, Title = "Inception", Price = 1299, Categories = ["Thriller"] }
        ]);
        _services.GetUserContextAsync(7).Returns(new UserOrderContext { PurchasedMovieIds = [1] });
    }

    private AgentService Create() => new(_services, _groq, new ConfigurationBuilder().Build(), NullLogger<AgentService>.Instance);

    private async Task<AgentChatResponse> RunTool(string tool, object args, string finalText = "Kész.")
    {
        _groq.SendAsync(Arg.Any<object>()).Returns(Groq.ToolCall(tool, args), Groq.Answer(finalText));
        return await Create().ProcessAsync("kérés", 7, "s1");
    }

    [Fact]
    public async Task AddToCart_FindsMovieAndReturnsCartAction()
    {
        _services.AddToCartAsync(2).Returns(true);

        var result = await RunTool("add_movie_to_cart", new { movie_title = "incep" });

        Assert.Equal("Kész.", result.Answer);
        Assert.Equal("agent", result.Source);
        Assert.Equal("cart_updated", result.Action!.Type);
        Assert.Equal("Inception", result.Action.Payload["movieTitle"]);
    }

    [Theory]
    [InlineData("add_movie_to_cart", "Titanic")]
    [InlineData("remove_movie_from_cart", "Titanic")]
    [InlineData("watch_movie", "Titanic")]
    [InlineData("start_watch_party", "Titanic")]
    [InlineData("add_movie_to_cart", "Inception")]
    [InlineData("remove_movie_from_cart", "Inception")]
    [InlineData("watch_movie", "Inception")]
    [InlineData("start_watch_party", "Inception")]
    [InlineData("search_movie", "Titanic")]
    [InlineData("unknown_tool", "x")]
    public async Task ToolsWithoutSuccess_ProduceNoAction(string tool, string title)
    {
        var result = await RunTool(tool, new { movie_title = title });

        Assert.Null(result.Action);
    }

    [Fact]
    public async Task RemoveFromCart_Watch_AndWatchParty_ForOwnedMovie()
    {
        _services.RemoveFromCartAsync(1).Returns(true);

        Assert.Equal("cart_updated", (await RunTool("remove_movie_from_cart", new { movie_title = "matrix" })).Action!.Type);
        Assert.Equal("/my-movies/1/watch", (await RunTool("watch_movie", new { movie_title = "matrix" })).Action!.Payload["path"]);
        Assert.Equal("/my-movies/1/watch-party", (await RunTool("start_watch_party", new { movie_title = "matrix" })).Action!.Payload["path"]);
    }

    [Fact]
    public async Task Navigate_DefaultsToHome()
    {
        Assert.Equal("auctions", (await RunTool("navigate_to_page", new { page = "auctions" })).Action!.Payload["page"]);
        Assert.Equal("home", (await RunTool("navigate_to_page", new { })).Action!.Payload["page"]);
    }

    [Fact]
    public async Task UpdateDisplayName_HandlesSuccessAndErrors()
    {
        _services.UpdateDisplayNameAsync("TheOne").Returns((true, (string?)null));
        _services.UpdateDisplayNameAsync("Ghost").Returns((false, (string?)"User not found"));
        _services.UpdateDisplayNameAsync("bad name").Returns((false, (string?)"invalid"));

        Assert.Equal("profile_updated", (await RunTool("update_display_name", new { new_name = "TheOne" })).Action!.Type);
        Assert.Null((await RunTool("update_display_name", new { new_name = "Ghost" })).Action);
        Assert.Null((await RunTool("update_display_name", new { new_name = "bad name" })).Action);
        Assert.Null((await RunTool("update_display_name", new { new_name = " " })).Action);
    }

    [Fact]
    public async Task SearchMovie_FeedsResultsBackToModel()
    {
        await RunTool("search_movie", new { movie_title = "" });

        var lastRequest = JsonNode.Parse(JsonSerializer.Serialize(_groq.ReceivedCalls().Last().GetArguments()[0]))!;
        var toolResult = lastRequest["messages"]!.AsArray().Last()!["content"]!.GetValue<string>();
        Assert.Contains("The Matrix | 799 Ft (discounted from 999 Ft) | Categories: Action", toolResult);
        Assert.Contains("Inception | 1299 Ft", toolResult);
    }

    [Fact]
    public async Task BillingAddress_UpdatesExistingOrCreates_AndValidates()
    {
        _services.GetAddressesAsync().Returns([new AddressInfo { Id = 3 }], new List<AddressInfo>());

        Assert.Equal("cart", (await RunTool("set_billing_address", new { street = "Fő u. 1", city = "Bp", zip = "1111" })).Action!.Payload["page"]);
        await _services.Received(1).UpdateAddressAsync(3, "Fő u. 1", "Bp", "1111");

        await RunTool("set_billing_address", new { street = "Fő u. 1", city = "Bp", zip = "1111" });
        await _services.Received(1).CreateAddressAsync("Fő u. 1", "Bp", "1111");

        Assert.Null((await RunTool("set_billing_address", new { street = "", city = "Bp", zip = "1111" })).Action);
    }

    [Fact]
    public async Task PlainAnswer_WithoutToolCall_AndEmptyAnswerGetsDefaultText()
    {
        _groq.SendAsync(Arg.Any<object>()).Returns(Groq.Answer("Szia!"));
        Assert.Equal("Szia!", (await Create().ProcessAsync("szia", 7, "s1")).Answer);

        _groq.SendAsync(Arg.Any<object>()).Returns(Groq.Answer(""));
        Assert.StartsWith("Done!", (await Create().ProcessAsync("szia", 7, "s1")).Answer);

        _groq.SendAsync(Arg.Any<object>()).Returns(JsonNode.Parse("""{"choices":[{"finish_reason":"tool_calls","message":{"tool_calls":[]}}]}""")!);
        Assert.StartsWith("Done!", (await Create().ProcessAsync("szia", 7, "s1")).Answer);
    }

    [Fact]
    public async Task FailedToolGeneration_IsExecutedFromErrorBody()
    {
        var error = JsonSerializer.Serialize(new
        {
            error = new { code = "tool_use_failed", failed_generation = "<function=navigate_to_page{\"page\": \"orders\"}</function>" }
        });
        _groq.SendAsync(Arg.Any<object>()).Returns<JsonNode>(_ => throw new GroqApiException(error));

        var result = await Create().ProcessAsync("go to orders", 7, "s1");

        Assert.Equal("Navigating to orders.", result.Answer);
        Assert.Equal("orders", result.Action!.Payload["page"]);
    }

    [Theory]
    [InlineData("{\"error\":{\"code\":\"rate_limit\"}}")]
    [InlineData("{\"error\":{\"code\":\"tool_use_failed\",\"failed_generation\":\"nincs függvény\"}}")]
    [InlineData("{\"error\":{\"code\":\"tool_use_failed\",\"failed_generation\":\"<function=search_movie{nem json}\"}}")]
    [InlineData("nem json")]
    public async Task UnrecoverableModelErrors_AreRethrown(string body)
    {
        _groq.SendAsync(Arg.Any<object>()).Returns<JsonNode>(_ => throw new GroqApiException(body));

        await Assert.ThrowsAsync<Exception>(() => Create().ProcessAsync("x", 7, "s1"));
    }

    [Fact]
    public async Task NotConfigured_Throws()
    {
        _groq.IsConfigured.Returns(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create().ProcessAsync("x", 7, "s1"));
    }
}
