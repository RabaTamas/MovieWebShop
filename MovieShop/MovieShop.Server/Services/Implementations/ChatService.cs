using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;
using System.Text;
using System.Text.Json;

namespace MovieShop.Server.Services.Implementations
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IConversationService _conversationService;

        public ChatService(AppDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory, IConversationService conversationService)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _conversationService = conversationService;
        }

        //public async Task<string> GetContextualAnswer(string question, string sessionId)
        //{
        //    var lowerQuestion = question.ToLower();

        //    // Check if question is about specific movie
        //    var movieContext = await GetMovieContext(lowerQuestion);
        //    if (!string.IsNullOrEmpty(movieContext))
        //    {
        //        return await GetAIResponseWithContext(question, movieContext);
        //    }

        //    // Check if question is about popular/trending movies
        //    if (lowerQuestion.Contains("popular") || lowerQuestion.Contains("trending") ||
        //        lowerQuestion.Contains("best selling") || lowerQuestion.Contains("top"))
        //    {
        //        var popularContext = await GetPopularMoviesContext();
        //        return await GetAIResponseWithContext(question, popularContext);
        //    }

        //    // Check if question is about categories
        //    if (lowerQuestion.Contains("genre") || lowerQuestion.Contains("category") ||
        //        lowerQuestion.Contains("action") || lowerQuestion.Contains("drama") ||
        //        lowerQuestion.Contains("comedy") || lowerQuestion.Contains("horror"))
        //    {
        //        var categoryContext = await GetCategoryContext(lowerQuestion);
        //        return await GetAIResponseWithContext(question, categoryContext);
        //    }

        //    // Default AI response without context
        //    return await GetAIResponseWithContext(question, "");
        //}

        public async Task<string> GetContextualAnswer(string question, string sessionId)
        {
            var lowerQuestion = question.ToLower();

            // Get conversation history
            var history = _conversationService.GetHistory(sessionId, 5);

            // Extract context from previous messages
            var previousContext = ExtractContextFromHistory(history);

            // Check if question refers to previous context
            if (IsReferencingPreviousContext(lowerQuestion) && !string.IsNullOrEmpty(previousContext))
            {
                question = ReplaceReferences(question, previousContext);
                lowerQuestion = question.ToLower();
            }

            // Check if question is about specific movie
            var movieContext = await GetMovieContext(lowerQuestion);
            if (!string.IsNullOrEmpty(movieContext))
            {
                var answer = await GetAIResponseWithContext(question, movieContext, history);
                _conversationService.AddMessage(sessionId, "user", question);
                _conversationService.AddMessage(sessionId, "assistant", answer);
                return answer;
            }

            // ÚJ: Check if user is asking about a movie we DON'T have
            if (lowerQuestion.Contains("do you have") || lowerQuestion.Contains("is there") ||
                lowerQuestion.Contains("available"))
            {
                // Extract potential movie title
                var potentialTitle = ExtractPotentialMovieTitle(lowerQuestion);
                if (!string.IsNullOrEmpty(potentialTitle))
                {
                    // Check if we have it
                    var exists = await _context.Movies.AnyAsync(m =>
                        m.Title.ToLower().Contains(potentialTitle.ToLower()) && !m.IsDeleted);

                    if (!exists)
                    {
                        var notFoundAnswer = $"Sorry, we don't have '{potentialTitle}' in our current inventory. You can browse our available movies on the website or contact us at support@movieshop.com to suggest additions!";
                        _conversationService.AddMessage(sessionId, "user", question);
                        _conversationService.AddMessage(sessionId, "assistant", notFoundAnswer);
                        return notFoundAnswer;
                    }
                }
            }

            // Popular movies
            if (lowerQuestion.Contains("popular") || lowerQuestion.Contains("trending") ||
                lowerQuestion.Contains("best selling") || lowerQuestion.Contains("top"))
            {
                var popularContext = await GetPopularMoviesContext();
                var answer = await GetAIResponseWithContext(question, popularContext, history);
                _conversationService.AddMessage(sessionId, "user", question);
                _conversationService.AddMessage(sessionId, "assistant", answer);
                return answer;
            }

            // Categories
            if (lowerQuestion.Contains("genre") || lowerQuestion.Contains("category") ||
                lowerQuestion.Contains("action") || lowerQuestion.Contains("drama") ||
                lowerQuestion.Contains("comedy") || lowerQuestion.Contains("horror"))
            {
                var categoryContext = await GetCategoryContext(lowerQuestion);
                var answer = await GetAIResponseWithContext(question, categoryContext, history);
                _conversationService.AddMessage(sessionId, "user", question);
                _conversationService.AddMessage(sessionId, "assistant", answer);
                return answer;
            }

            // Default
            var defaultAnswer = await GetAIResponseWithContext(question, "", history);
            _conversationService.AddMessage(sessionId, "user", question);
            _conversationService.AddMessage(sessionId, "assistant", defaultAnswer);
            return defaultAnswer;
        }

        private string ExtractPotentialMovieTitle(string question)
        {
            // Pattern: "Do you have [MovieTitle]"
            var match = System.Text.RegularExpressions.Regex.Match(
                question,
                @"(?:do you have|is there|got)\s+(?:the\s+)?([A-Z][A-Za-z0-9\s:]+?)(?:\s+(?:movie|film|available)|\?|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return string.Empty;
        }


        private async Task<string> GetMovieContext(string question)
        {
            // Try to find movie title in the question
            var movies = await _context.Movies
                .Where(m => !m.IsDeleted)
                .Include(m => m.Reviews)
                    .ThenInclude(r => r.User)
                .Include(m => m.Categories)
                .ToListAsync();

            var foundMovie = movies.FirstOrDefault(m =>
                question.Contains(m.Title.ToLower()));

            if (foundMovie == null)
                return string.Empty;

            var context = new StringBuilder();
            context.AppendLine($"IMPORTANT: Answer ONLY based on this verified information:");
            context.AppendLine($"Movie: {foundMovie.Title}");
            context.AppendLine($"Description: {foundMovie.Description}");
            context.AppendLine($"Price: {foundMovie.DiscountedPrice ?? foundMovie.Price} Ft");
            context.AppendLine($"Categories: {string.Join(", ", foundMovie.Categories?.Select(c => c.Name) ?? new List<string>())}");
            context.AppendLine($"Status: Available in our webshop");

            if (foundMovie.Reviews != null && foundMovie.Reviews.Any())
            {
                context.AppendLine($"\nREAL REVIEWS FROM OUR WEBSHOP ({foundMovie.Reviews.Count} total):");
                foreach (var review in foundMovie.Reviews.Take(5))
                {
                    context.AppendLine($"- Review by {review.User?.UserName ?? "Anonymous"}: \"{review.Content}\"");
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
            var topMovies = await _context.OrderMovies
                .Include(om => om.Movie)
                .Where(om => !om.Movie.IsDeleted)
                .GroupBy(om => new { om.MovieId, om.Movie.Title, om.Movie.Price, om.Movie.DiscountedPrice })
                .Select(g => new
                {
                    Title = g.Key.Title,
                    OrderCount = g.Sum(om => om.Quantity),
                    Price = g.Key.DiscountedPrice ?? g.Key.Price
                })
                .OrderByDescending(m => m.OrderCount)
                .Take(5)
                .ToListAsync();

            if (!topMovies.Any())
            {
                return "We have a great selection of movies, but no sales data yet.";
            }

            var context = new StringBuilder("Top 5 Most Popular Movies:\n");
            foreach (var movie in topMovies)
            {
                context.AppendLine($"- {movie.Title} ({movie.OrderCount} orders, {movie.Price} Ft)");
            }

            return context.ToString();
        }

        private async Task<string> GetCategoryContext(string question)
        {
            var categories = await _context.Categories
                .Include(c => c.Movies)
                .ToListAsync();

            var matchedCategory = categories.FirstOrDefault(c =>
                question.Contains(c.Name.ToLower()));

            if (matchedCategory == null)
            {
                var allCategories = string.Join(", ", categories.Select(c => c.Name));
                return $"Available categories: {allCategories}";
            }

            var movieCount = matchedCategory.Movies?.Count(m => !m.IsDeleted) ?? 0;
            var context = $"Category: {matchedCategory.Name}\n";
            context += $"Available movies: {movieCount}\n";

            if (movieCount > 0)
            {
                var topMovies = matchedCategory.Movies?
                    .Where(m => !m.IsDeleted)
                    .Take(5)
                    .Select(m => $"- {m.Title} ({m.DiscountedPrice ?? m.Price} Ft)")
                    .ToList();

                context += "Featured movies:\n" + string.Join("\n", topMovies ?? new List<string>());
            }

            return context;
        }

        private string ExtractContextFromHistory(List<ConversationMessage> history)
        {
            // Look for movie titles in previous messages
            var lastMovieMention = history
                .Where(m => m.Role == "assistant")
                .SelectMany(m => ExtractMovieTitles(m.Content))
                .LastOrDefault();

            return lastMovieMention ?? string.Empty;
        }

        private List<string> ExtractMovieTitles(string text)
        {
            // Simple extraction - look for quoted text or common movie patterns
            var titles = new List<string>();

            // Pattern: "MovieTitle"
            var matches = System.Text.RegularExpressions.Regex.Matches(text, @"""([^""]+)""");
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                titles.Add(match.Groups[1].Value);
            }

            // Pattern: movie "MovieTitle" or film "MovieTitle"
            matches = System.Text.RegularExpressions.Regex.Matches(text, @"(?:movie|film)\s+[""']?([A-Z][^.!?,""']+)[""']?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                titles.Add(match.Groups[1].Value.Trim());
            }

            return titles.Distinct().ToList();
        }


        private bool IsReferencingPreviousContext(string question)
        {
            var references = new[] { "it", "that", "this", "the movie", "the film", "that one", "this one" };
            return references.Any(r => question.Contains(r, StringComparison.OrdinalIgnoreCase));
        }

        private string ReplaceReferences(string question, string context)
        {
            var lowerQuestion = question.ToLower();

            if (lowerQuestion.Contains("it") || lowerQuestion.Contains("that") || lowerQuestion.Contains("this"))
            {
                question = System.Text.RegularExpressions.Regex.Replace(
                    question,
                    @"\b(it|that|this)\b",
                    context,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return question;
        }


        private async Task<string> GetAIResponseWithContext(string question, string context, List<ConversationMessage> history)
        {
            try
            {
                var apiKey = _configuration["Groq:ApiKey"];
                var url = "https://api.groq.com/openai/v1/chat/completions";

                var systemPrompt = @"You are MovieShop customer service assistant. Follow these rules STRICTLY:

                    CRITICAL RULES:
                    1. ONLY answer based on the provided Context data
                    2. NEVER make up or invent information
                    3. If Context shows 'No reviews yet' - say there are NO reviews, don't invent any
                    4. If a movie is NOT in the Context - say 'We don't have that movie' or 'Let me check our inventory'
                    5. Do NOT hallucinate movie details, reviews, or availability
                    6. Be helpful but HONEST - if you don't know something, say so

                    Store info:
                    - Payment: Stripe card payment
                    - Shipping: 3-5 business days  
                    - Returns: 14 days
                    - Contact: support@movieshop.com

                    Answer in English, briefly (max 4-5 sentences).";

                // Build messages array with history
                var messages = new List<object>
                {
                    new { role = "system", content = systemPrompt }
                };

                // Add recent conversation history (last 3 messages only)
                foreach (var msg in history.TakeLast(3))
                {
                    messages.Add(new { role = msg.Role, content = msg.Content });
                }

                // Add current question with context
                var userPrompt = string.IsNullOrEmpty(context)
                    ? question
                    : $"=== VERIFIED DATA FROM DATABASE ===\n{context}\n\n=== USER QUESTION ===\n{question}\n\nRemember: ONLY use the information from VERIFIED DATA above. Do NOT invent anything.";

                messages.Add(new { role = "user", content = userPrompt });

                var requestBody = new
                {
                    model = "llama-3.1-8b-instant",
                    messages = messages.ToArray(),
                    max_tokens = 250,
                    temperature = 0.3, // ALACSONYABB = kevesebb kreativitás = kevesebb hallucináció
                    top_p = 0.9
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Groq API error: {responseBody}");
                    return "Sorry, I'm having trouble right now. Email: support@movieshop.com";
                }

                var jsonDoc = JsonDocument.Parse(responseBody);
                var answer = jsonDoc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()?.Trim();

                return answer ?? "I couldn't generate a response. Email: support@movieshop.com";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AI Error: {ex.Message}");
                return "Technical error. Email: support@movieshop.com";
            }
        }
    }
}