using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MovieShop.ChatService.Models;

namespace MovieShop.ChatService.Services;

public interface IAgentService
{
    Task<AgentChatResponse> ProcessAsync(string question, int userId, string sessionId);
}

/// <summary>
/// A monolit AgentService-ének hű átirata: ugyanaz az agentic ciklus, ugyanazok a
/// nyolc eszköz eredményszövegei és AgentAction payloadjai (a kliens Chatbot.jsx
/// ezekre a kulcsokra épít: `page`, `path`, `movieTitle`, `newName`).
///
/// Az eszközök végrehajtása REST-hívásokká vált:
///   add/remove_movie_from_cart → Catalog (keresés) + Order (kosár)
///   search_movie               → Catalog
///   watch_movie, start_watch_party → Catalog (keresés) + Order (birtoklás)
///   update_display_name, set_billing_address → User
///   navigate_to_page           → nincs hálózati hívás
/// </summary>
public partial class AgentService : IAgentService
{
    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IServiceClients _services;
    private readonly IGroqClient _groq;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentService> _logger;

    public AgentService(
        IServiceClients services,
        IGroqClient groq,
        IConfiguration configuration,
        ILogger<AgentService> logger)
    {
        _services = services;
        _groq = groq;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AgentChatResponse> ProcessAsync(string question, int userId, string sessionId)
    {
        if (!_groq.IsConfigured)
            throw new InvalidOperationException("Groq API key not configured");

        var messages = new List<object>
        {
            new { role = "system", content = AgentTools.SystemPrompt },
            new { role = "user", content = question }
        };

        AgentAction? finalAction = null;
        var finalText = string.Empty;

        // ── Agentic ciklus (legfeljebb 3 kör) ────────────────────────────────
        for (var turn = 0; turn < 3; turn++)
        {
            var requestBody = new
            {
                // A Groq kivezette a llama-3.3-70b-versatile modellt, ezért konfigurálható
                model = _configuration["Groq:AgentModel"] ?? "openai/gpt-oss-120b",
                max_tokens = 1024,
                tools = AgentTools.Definitions,
                messages
            };

            JsonNode doc;
            try
            {
                doc = await _groq.SendAsync(requestBody);
            }
            catch (GroqApiException ex)
            {
                // Egyes modellek <function=name{...}> formában generálnak eszközhívást
                // JSON helyett — ilyenkor a hibatörzsből hajtjuk végre kézzel.
                var failedGen = ex.GetFailedGeneration();
                if (failedGen != null)
                {
                    var fallback = await TryFallbackExecutionAsync(failedGen, userId);
                    if (fallback.HasValue)
                    {
                        if (fallback.Value.action != null) finalAction = fallback.Value.action;
                        finalText = fallback.Value.text;
                        break;
                    }
                }

                throw new Exception($"Groq API error: {ex.ResponseJson}");
            }

            var choice = doc["choices"]![0]!;
            var message = choice["message"]!;
            var finishReason = choice["finish_reason"]?.GetValue<string>();

            var content = message["content"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(content))
                finalText = content;

            if (finishReason != "tool_calls") break;

            var toolCalls = message["tool_calls"]?.AsArray();
            if (toolCalls == null || toolCalls.Count == 0) break;

            messages.Add(new
            {
                role = "assistant",
                content = message["content"]?.GetValue<string>() ?? (object?)null,
                tool_calls = toolCalls.Deserialize<object[]>(SnakeCase)
            });

            foreach (var toolCall in toolCalls)
            {
                var toolCallId = toolCall!["id"]!.GetValue<string>();
                var toolName = toolCall["function"]!["name"]!.GetValue<string>();
                var argsJson = toolCall["function"]!["arguments"]!.GetValue<string>();
                var input = JsonNode.Parse(argsJson)!.AsObject();

                var (resultText, action) = await ExecuteToolAsync(toolName, input, userId);
                if (action != null) finalAction = action;

                messages.Add(new { role = "tool", tool_call_id = toolCallId, content = resultText });
            }
        }

        return new AgentChatResponse
        {
            Answer = string.IsNullOrWhiteSpace(finalText) ? "Done! Let me know if you need anything else." : finalText,
            Source = "agent",
            Action = finalAction
        };
    }

    // ── Eszközök végrehajtása ────────────────────────────────────────────────

    private async Task<(string result, AgentAction? action)> ExecuteToolAsync(
        string toolName, JsonObject input, int userId)
    {
        return toolName switch
        {
            "add_movie_to_cart" => await AddToCartAsync(input),
            "remove_movie_from_cart" => await RemoveFromCartAsync(input),
            "navigate_to_page" => ExecuteNavigate(input),
            "update_display_name" => await UpdateDisplayNameAsync(input),
            "search_movie" => await SearchMovieAsync(input),
            "watch_movie" => await WatchMovieAsync(input, userId),
            "start_watch_party" => await StartWatchPartyAsync(input, userId),
            "set_billing_address" => await SetBillingAddressAsync(input),
            _ => ("Unknown tool", null)
        };
    }

    /// <summary>A monolit keresési szabálya: a cím tartalmazza a keresett szöveget (kis-nagybetű független).</summary>
    private async Task<List<CatalogMovie>> FindMoviesAsync(string title)
    {
        var catalog = await _services.GetCatalogAsync();
        return catalog.Where(m => m.Title.ToLower().Contains(title.ToLower())).ToList();
    }

    private async Task<(string, AgentAction?)> AddToCartAsync(JsonObject input)
    {
        var title = input["movie_title"]?.GetValue<string>() ?? "";
        var movie = (await FindMoviesAsync(title)).FirstOrDefault();

        if (movie == null)
            return ($"Movie '{title}' not found in our catalog.", null);

        if (!await _services.AddToCartAsync(movie.Id))
            return ($"Could not add '{movie.Title}' to cart (you may already own it or it's already in your cart).", null);

        var action = new AgentAction
        {
            Type = "cart_updated",
            Payload = new() { ["movieTitle"] = movie.Title, ["movieId"] = movie.Id }
        };
        return ($"Successfully added '{movie.Title}' to your cart.", action);
    }

    private async Task<(string, AgentAction?)> RemoveFromCartAsync(JsonObject input)
    {
        var title = input["movie_title"]?.GetValue<string>() ?? "";
        var movie = (await FindMoviesAsync(title)).FirstOrDefault();

        if (movie == null)
            return ($"Movie '{title}' not found.", null);

        if (!await _services.RemoveFromCartAsync(movie.Id))
            return ($"'{movie.Title}' was not in your cart.", null);

        var action = new AgentAction
        {
            Type = "cart_updated",
            Payload = new() { ["movieTitle"] = movie.Title }
        };
        return ($"Removed '{movie.Title}' from your cart.", action);
    }

    private static (string, AgentAction?) ExecuteNavigate(JsonObject input)
    {
        var page = input["page"]?.GetValue<string>() ?? "home";
        var action = new AgentAction
        {
            Type = "navigate",
            Payload = new() { ["page"] = page }
        };
        return ($"Navigating to {page}.", action);
    }

    private async Task<(string, AgentAction?)> UpdateDisplayNameAsync(JsonObject input)
    {
        var newName = input["new_name"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(newName))
            return ("Name cannot be empty.", null);

        var (success, error) = await _services.UpdateDisplayNameAsync(newName);
        if (!success)
        {
            return error == "User not found"
                ? ("User not found.", null)
                : ($"Failed to update name: {error}", null);
        }

        var action = new AgentAction
        {
            Type = "profile_updated",
            Payload = new() { ["newName"] = newName }
        };
        return ($"Your display name has been updated to '{newName}'.", action);
    }

    private async Task<(string, AgentAction?)> SearchMovieAsync(JsonObject input)
    {
        var title = input["movie_title"]?.GetValue<string>() ?? "";
        var movies = (await FindMoviesAsync(title)).Take(3).ToList();

        if (movies.Count == 0)
            return ($"No movie found matching '{title}' in our catalog.", null);

        var sb = new StringBuilder();
        foreach (var m in movies)
        {
            var price = m.DiscountedPrice.HasValue
                ? $"{m.DiscountedPrice} Ft (discounted from {m.Price} Ft)"
                : $"{m.Price} Ft";
            var cats = string.Join(", ", m.Categories);
            sb.AppendLine($"- {m.Title} | {price} | Categories: {cats}");
        }

        return ($"Found in our catalog:\n{sb}", null);
    }

    private async Task<bool> OwnsMovieAsync(int userId, int movieId)
    {
        // A monolit bármely rendelésben szereplő filmet birtokoltnak tekintett
        var context = await _services.GetUserContextAsync(userId);
        return context?.PurchasedMovieIds.Contains(movieId) == true;
    }

    private async Task<(string, AgentAction?)> WatchMovieAsync(JsonObject input, int userId)
    {
        var title = input["movie_title"]?.GetValue<string>() ?? "";
        var movie = (await FindMoviesAsync(title)).FirstOrDefault();

        if (movie == null)
            return ($"Movie '{title}' not found in our catalog.", null);

        if (!await OwnsMovieAsync(userId, movie.Id))
            return ($"You don't own '{movie.Title}' yet. Add it to your cart to purchase it!", null);

        var action = new AgentAction
        {
            Type = "navigate",
            Payload = new() { ["path"] = $"/my-movies/{movie.Id}/watch" }
        };
        return ($"Starting '{movie.Title}' now!", action);
    }

    private async Task<(string, AgentAction?)> StartWatchPartyAsync(JsonObject input, int userId)
    {
        var title = input["movie_title"]?.GetValue<string>() ?? "";
        var movie = (await FindMoviesAsync(title)).FirstOrDefault();

        if (movie == null)
            return ($"Movie '{title}' not found in our catalog.", null);

        if (!await OwnsMovieAsync(userId, movie.Id))
            return ($"You don't own '{movie.Title}' yet. Purchase it first to start a watch party!", null);

        var action = new AgentAction
        {
            Type = "navigate",
            Payload = new() { ["path"] = $"/my-movies/{movie.Id}/watch-party" }
        };
        return ($"Starting a watch party for '{movie.Title}'! Invite your friends.", action);
    }

    private async Task<(string text, AgentAction? action)?> TryFallbackExecutionAsync(string failedGeneration, int userId)
    {
        var match = FunctionCallPattern().Match(failedGeneration);
        if (!match.Success) return null;

        var toolName = match.Groups[1].Value;
        var argsJson = match.Groups[2].Value;

        try
        {
            var input = JsonNode.Parse(argsJson)?.AsObject();
            if (input == null) return null;

            var (resultText, action) = await ExecuteToolAsync(toolName, input, userId);
            return (resultText, action);
        }
        catch
        {
            return null;
        }
    }

    private async Task<(string, AgentAction?)> SetBillingAddressAsync(JsonObject input)
    {
        var street = input["street"]?.GetValue<string>() ?? "";
        var city = input["city"]?.GetValue<string>() ?? "";
        var zip = input["zip"]?.GetValue<string>() ?? "";

        if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(zip))
            return ("Please provide street, city, and zip code.", null);

        var navigateAction = new AgentAction
        {
            Type = "navigate",
            Payload = new() { ["page"] = "cart" }
        };

        var existing = await _services.GetAddressesAsync();
        if (existing.Count > 0)
        {
            await _services.UpdateAddressAsync(existing[0].Id, street, city, zip);
            return ($"Your billing address has been updated to: {street}, {city} {zip}. Taking you to your cart.", navigateAction);
        }

        await _services.CreateAddressAsync(street, city, zip);
        return ($"Your billing address has been set to: {street}, {city} {zip}. Taking you to your cart.", navigateAction);
    }

    [GeneratedRegex(@"<function=(\w+)(\{.*?\})", RegexOptions.Singleline)]
    private static partial Regex FunctionCallPattern();
}
