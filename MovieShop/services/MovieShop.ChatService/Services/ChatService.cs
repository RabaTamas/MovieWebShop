using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MovieShop.ChatService.Models;

namespace MovieShop.ChatService.Services;

public interface IChatService
{
    Task<string> GetContextualAnswer(string question, string sessionId, string? userId = null);
}

/// <summary>
/// A monolit ChatService-ének hű átirata. Az ágak, a prompt-szabályok, a
/// kontextusformátumok és a hibaüzenetek szó szerint egyeznek; kizárólag az
/// adatforrás változott: a DbContext-lekérdezések helyén REST-hívások vannak
/// a Catalog, az Order és a User Service felé.
/// </summary>
public class ChatService : IChatService
{
    private readonly IServiceClients _services;
    private readonly IConversationService _conversationService;
    private readonly IGroqClient _groq;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IServiceClients services,
        IConversationService conversationService,
        IGroqClient groq,
        IConfiguration configuration,
        ILogger<ChatService> logger)
    {
        _services = services;
        _conversationService = conversationService;
        _groq = groq;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetContextualAnswer(string question, string sessionId, string? userId = null)
    {
        var lowerQuestion = question.ToLower();

        // Felhasználói kontextus, ha be van jelentkezve
        var userContext = string.Empty;
        UserOrderContext? orderContext = null;

        if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var parsedUserId))
        {
            orderContext = await _services.GetUserContextAsync(parsedUserId);
            userContext = await GetUserContext(parsedUserId, orderContext);
        }

        var history = _conversationService.GetHistory(sessionId, 5);

        var previousContext = ExtractContextFromHistory(history);

        if (IsReferencingPreviousContext(lowerQuestion) && !string.IsNullOrEmpty(previousContext))
        {
            question = ReplaceReferences(question, previousContext);
            lowerQuestion = question.ToLower();
        }

        // A monolit ágankként külön lekérdezést futtatott a filmekre; itt egyetlen
        // hívás hozza a katalógust, és minden ág ebből dolgozik.
        var catalog = await _services.GetCatalogAsync();

        // Konkrét filmre vonatkozó kérdés
        var movieContext = GetMovieContext(lowerQuestion, catalog);
        if (!string.IsNullOrEmpty(movieContext))
        {
            var combinedContext = CombineContexts(userContext, movieContext);
            return await AnswerAndRememberAsync(sessionId, question, combinedContext, history);
        }

        // Olyan filmre kérdez, ami nincs a kínálatban
        if (lowerQuestion.Contains("do you have") || lowerQuestion.Contains("is there") ||
            lowerQuestion.Contains("available"))
        {
            var potentialTitle = ExtractPotentialMovieTitle(lowerQuestion);
            if (!string.IsNullOrEmpty(potentialTitle))
            {
                var exists = catalog.Any(m => m.Title.ToLower().Contains(potentialTitle.ToLower()));

                if (!exists)
                {
                    var notFoundAnswer = $"Sorry, we don't have '{potentialTitle}' in our current inventory. You can browse our available movies on the website or contact us at support@movieshop.com to suggest additions!";
                    _conversationService.AddMessage(sessionId, "user", question);
                    _conversationService.AddMessage(sessionId, "assistant", notFoundAnswer);
                    return notFoundAnswer;
                }
            }
        }

        // Személyre szabott ajánlások
        if (!string.IsNullOrEmpty(userId) &&
            (lowerQuestion.Contains("recommend") || lowerQuestion.Contains("suggest") ||
             lowerQuestion.Contains("what should i watch") || lowerQuestion.Contains("what should i buy") ||
             lowerQuestion.Contains("similar") || lowerQuestion.Contains("like what i") ||
             lowerQuestion.Contains("beyond") || lowerQuestion.Contains("other movie")))
        {
            if (int.TryParse(userId, out _))
            {
                var recContext = orderContext?.RecommendationContext ?? string.Empty;
                var purchasedIds = orderContext?.PurchasedMovieIds ?? [];

                var availableMovies = catalog
                    .Where(m => !purchasedIds.Contains(m.Id))
                    .OrderBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(m => new { m.Title, Price = m.DiscountedPrice ?? m.Price })
                    .ToList();

                var catalogSb = new StringBuilder();
                catalogSb.AppendLine("=== MOVIES AVAILABLE IN OUR SHOP (not yet purchased by user) ===");
                catalogSb.AppendLine("ONLY suggest movies from this list — do NOT invent movies outside it:");
                foreach (var m in availableMovies)
                    catalogSb.AppendLine($"- {m.Title} ({m.Price} Ft)");

                var combinedContext = CombineContexts(userContext, CombineContexts(recContext, catalogSb.ToString()));
                return await AnswerAndRememberAsync(sessionId, question, combinedContext, history);
            }
        }

        // Népszerű filmek
        if (lowerQuestion.Contains("popular") || lowerQuestion.Contains("trending") ||
            lowerQuestion.Contains("best selling") || lowerQuestion.Contains("top"))
        {
            var popularContext = await GetPopularMoviesContext();
            var combinedContext = CombineContexts(userContext, popularContext);
            return await AnswerAndRememberAsync(sessionId, question, combinedContext, history);
        }

        // Kategóriák
        if (lowerQuestion.Contains("genre") || lowerQuestion.Contains("category") ||
            lowerQuestion.Contains("action") || lowerQuestion.Contains("drama") ||
            lowerQuestion.Contains("comedy") || lowerQuestion.Contains("horror"))
        {
            var categoryContext = await GetCategoryContext(lowerQuestion, catalog);
            var combinedContext = CombineContexts(userContext, categoryContext);
            return await AnswerAndRememberAsync(sessionId, question, combinedContext, history);
        }

        // Alapértelmezett
        var defaultContext = CombineContexts(userContext, "");
        return await AnswerAndRememberAsync(sessionId, question, defaultContext, history);
    }

    private async Task<string> AnswerAndRememberAsync(
        string sessionId, string question, string context, List<ConversationMessage> history)
    {
        var answer = await GetAIResponseWithContext(question, context, history);
        _conversationService.AddMessage(sessionId, "user", question);
        _conversationService.AddMessage(sessionId, "assistant", answer);
        return answer;
    }

    private static string ExtractPotentialMovieTitle(string question)
    {
        // Minta: "Do you have [MovieTitle]"
        var match = Regex.Match(
            question,
            @"(?:do you have|is there|got)\s+(?:the\s+)?([A-Z][A-Za-z0-9\s:]+?)(?:\s+(?:movie|film|available)|\?|$)",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static string GetMovieContext(string question, List<CatalogMovie> movies)
    {
        var foundMovie = movies.FirstOrDefault(m => question.Contains(m.Title.ToLower()));

        if (foundMovie == null)
            return string.Empty;

        var context = new StringBuilder();
        context.AppendLine($"IMPORTANT: Answer ONLY based on this verified information:");
        context.AppendLine($"Movie: {foundMovie.Title}");
        context.AppendLine($"Description: {foundMovie.Description}");
        context.AppendLine($"Price: {foundMovie.DiscountedPrice ?? foundMovie.Price} Ft");
        context.AppendLine($"Categories: {string.Join(", ", foundMovie.Categories)}");
        context.AppendLine($"Status: Available in our webshop");

        if (foundMovie.Reviews.Count > 0)
        {
            context.AppendLine($"\nREAL REVIEWS FROM OUR WEBSHOP ({foundMovie.Reviews.Count} total):");
            foreach (var review in foundMovie.Reviews.Take(5))
            {
                var author = string.IsNullOrEmpty(review.UserName) ? "Anonymous" : review.UserName;
                context.AppendLine($"- Review by {author}: \"{review.Content}\"");
            }
        }
        else
        {
            context.AppendLine("\nREVIEWS: No reviews yet in our webshop.");
        }

        context.AppendLine("\nDO NOT make up or invent reviews. Only mention the reviews listed above.");

        return context.ToString();
    }

    private async Task<string> GetPopularMoviesContext()
    {
        var topMovies = await _services.GetTopMoviesAsync(5);

        if (topMovies.Count == 0)
            return "We have a great selection of movies, but no sales data yet.";

        var context = new StringBuilder("Top 5 Most Popular Movies:\n");
        foreach (var movie in topMovies)
            context.AppendLine($"- {movie.Title} ({movie.OrderCount} orders, {movie.Price} Ft)");

        return context.ToString();
    }

    private async Task<string> GetCategoryContext(string question, List<CatalogMovie> catalog)
    {
        var categories = await _services.GetCategoriesAsync();

        var matchedCategory = categories.FirstOrDefault(c => question.Contains(c.Name.ToLower()));

        if (matchedCategory == null)
        {
            var allCategories = string.Join(", ", categories.Select(c => c.Name));
            return $"Available categories: {allCategories}";
        }

        var moviesInCategory = catalog.Where(m => m.Categories.Contains(matchedCategory.Name)).ToList();
        var movieCount = moviesInCategory.Count;

        var context = $"Category: {matchedCategory.Name}\n";
        context += $"Available movies: {movieCount}\n";

        if (movieCount > 0)
        {
            var topMovies = moviesInCategory
                .Take(5)
                .Select(m => $"- {m.Title} ({m.DiscountedPrice ?? m.Price} Ft)")
                .ToList();

            context += "Featured movies:\n" + string.Join("\n", topMovies);
        }

        return context;
    }

    private static string ExtractContextFromHistory(List<ConversationMessage> history)
    {
        var lastMovieMention = history
            .Where(m => m.Role == "assistant")
            .SelectMany(m => ExtractMovieTitles(m.Content))
            .LastOrDefault();

        return lastMovieMention ?? string.Empty;
    }

    private static List<string> ExtractMovieTitles(string text)
    {
        var titles = new List<string>();

        // Minta: "MovieTitle"
        foreach (Match match in Regex.Matches(text, @"""([^""]+)"""))
            titles.Add(match.Groups[1].Value);

        // Minta: movie "MovieTitle" vagy film "MovieTitle"
        foreach (Match match in Regex.Matches(text, @"(?:movie|film)\s+[""']?([A-Z][^.!?,""']+)[""']?", RegexOptions.IgnoreCase))
            titles.Add(match.Groups[1].Value.Trim());

        return titles.Distinct().ToList();
    }

    private static bool IsReferencingPreviousContext(string question)
    {
        var references = new[] { "it", "that", "this", "the movie", "the film", "that one", "this one" };
        return references.Any(r => question.Contains(r, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReplaceReferences(string question, string context)
    {
        var lowerQuestion = question.ToLower();

        if (lowerQuestion.Contains("it") || lowerQuestion.Contains("that") || lowerQuestion.Contains("this"))
        {
            question = Regex.Replace(question, @"\b(it|that|this)\b", context, RegexOptions.IgnoreCase);
        }

        return question;
    }

    private async Task<string> GetAIResponseWithContext(string question, string context, List<ConversationMessage> history)
    {
        try
        {
            var systemPrompt = @"You are MovieShop customer service assistant. Follow these rules STRICTLY:

                    CRITICAL RULES:
                    1. ONLY answer based on the provided Context data (includes USER PERSONAL DATA if user is logged in)
                    2. NEVER make up or invent information
                    3. If Context shows 'No reviews yet' - say there are NO reviews, don't invent any
                    4. If a movie is NOT in the Context - say 'We don't have that movie' or 'Let me check our inventory'
                    5. Do NOT hallucinate movie details, reviews, or availability
                    6. Be helpful but HONEST - if you don't know something, say so
                    7. If USER PERSONAL DATA is provided, use it to give personalized answers:
                       - For cart questions: refer to their actual cart items
                       - For order questions: refer to their actual orders with order numbers and dates
                       - For purchase history: refer to movies they actually bought
                       - Be friendly and use 'your' instead of generic terms

                    Store info:
                    - Payment: Stripe card payment
                    - Digital streaming platform: Instant access after purchase
                    - Watch movies online in My Movies section
                    - Refunds: 14 days if not watched
                    - Contact: support@movieshop.com

                    Answer in English, briefly (max 4-5 sentences).";

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            // A legutóbbi 3 üzenet a beszélgetésből
            foreach (var msg in history.TakeLast(3))
                messages.Add(new { role = msg.Role, content = msg.Content });

            var userPrompt = string.IsNullOrEmpty(context)
                ? question
                : $"=== VERIFIED DATA FROM DATABASE ===\n{context}\n\n=== USER QUESTION ===\n{question}\n\nRemember: ONLY use the information from VERIFIED DATA above. Do NOT invent anything.";

            messages.Add(new { role = "user", content = userPrompt });

            var requestBody = new
            {
                // A Groq kivezette a llama-3.1-8b-instant modellt, ezért konfigurálható
                model = _configuration["Groq:ChatModel"] ?? "openai/gpt-oss-20b",
                messages = messages.ToArray(),
                max_tokens = 250,
                temperature = 0.3,
                top_p = 0.9
            };

            JsonNode response;
            try
            {
                response = await _groq.SendAsync(requestBody);
            }
            catch (GroqApiException ex)
            {
                _logger.LogError("Groq API error: {Response}", ex.ResponseJson);
                return "Sorry, I'm having trouble right now. Email: support@movieshop.com";
            }

            var answer = response["choices"]?[0]?["message"]?["content"]?.GetValue<string>()?.Trim();

            return answer ?? "I couldn't generate a response. Email: support@movieshop.com";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI error");
            return "Technical error. Email: support@movieshop.com";
        }
    }

    private async Task<string> GetUserContext(int userId, UserOrderContext? orderContext)
    {
        try
        {
            var context = new StringBuilder();
            context.AppendLine("=== USER PERSONAL DATA ===");

            var user = await _services.GetUserInfoAsync(userId);
            if (user != null)
                context.AppendLine($"User: {user.UserName} ({user.Email})");

            // Ha az Order Service nem elérhető, a kosár- és rendelésszekció kimarad,
            // ahelyett hogy tévesen „üres kosarat" közölnénk a modellel.
            if (orderContext == null)
                return context.ToString();

            if (orderContext.CartItems.Count > 0)
            {
                context.AppendLine($"\nSHOPPING CART ({orderContext.CartItems.Count} items):");
                foreach (var item in orderContext.CartItems)
                    context.AppendLine($"- {item.Title} (Quantity: {item.Quantity}, Price: {item.Price} Ft each)");

                context.AppendLine($"Cart Total: {orderContext.CartItems.Sum(i => i.Quantity * i.Price)} Ft");
            }
            else
            {
                context.AppendLine("\nSHOPPING CART: Empty");
            }

            if (orderContext.RecentOrders.Count > 0)
            {
                context.AppendLine($"\nRECENT ORDERS ({orderContext.RecentOrders.Count} orders):");
                foreach (var order in orderContext.RecentOrders)
                {
                    var localOrderDate = order.OrderDate.ToLocalTime();
                    context.AppendLine($"- Order #{order.Id} placed on {localOrderDate:yyyy-MM-dd HH:mm}:");
                    context.AppendLine($"  Status: {order.Status}");
                    context.AppendLine($"  Total: {order.TotalPrice} Ft");
                    context.AppendLine($"  Movies: {string.Join(", ", order.Movies)}");
                }
            }
            else
            {
                context.AppendLine("\nRECENT ORDERS: No orders yet");
            }

            if (orderContext.PurchasedTitles.Count > 0)
            {
                context.AppendLine($"\nALL PURCHASED MOVIES ({orderContext.PurchasedTitles.Count} unique titles):");
                context.AppendLine(string.Join(", ", orderContext.PurchasedTitles));
            }

            return context.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user context");
            return string.Empty;
        }
    }

    private static string CombineContexts(string userContext, string otherContext)
    {
        if (string.IsNullOrEmpty(userContext) && string.IsNullOrEmpty(otherContext))
            return string.Empty;

        var combined = new StringBuilder();

        if (!string.IsNullOrEmpty(userContext))
        {
            combined.AppendLine(userContext);
            combined.AppendLine();
        }

        if (!string.IsNullOrEmpty(otherContext))
            combined.AppendLine(otherContext);

        return combined.ToString();
    }
}
