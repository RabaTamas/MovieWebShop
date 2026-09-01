using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MovieShop.Server.Services.Implementations
{
    /// <summary>
    /// Agentic chatbot service using Groq API with Tool Calling (OpenAI-compatible format).
    /// Handles write operations: add to cart, remove from cart, navigate, update profile.
    /// Simple queries are still handled by the Groq/Llama ChatService.
    /// </summary>
    public class AgentService : IAgentService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IShoppingCartService _cartService;
        private readonly UserManager<User> _userManager;
        private readonly AppDbContext _db;
        private readonly IAddressService _addressService;

        private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        // ── Tool definitions (OpenAI function calling format) ─────────
        private static readonly object[] Tools =
        [
            new {
                type = "function",
                function = new {
                    name = "add_movie_to_cart",
                    description = "Add a movie to the user's shopping cart. Use this when the user says things like 'add X to cart', 'I want to buy X', 'put X in my cart'.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            movie_title = new { type = "string", description = "The title of the movie to add to the cart" }
                        },
                        required = new[] { "movie_title" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "remove_movie_from_cart",
                    description = "Remove a movie from the user's shopping cart.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            movie_title = new { type = "string", description = "The title of the movie to remove" }
                        },
                        required = new[] { "movie_title" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "navigate_to_page",
                    description = "Navigate the user to a page. Use ONLY for: cart, checkout, profile, orders, home, auctions. Do NOT use for my-movies, watch, or watch-party - use watch_movie or start_watch_party tools instead.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            page = new {
                                type = "string",
                                description = "Page to navigate to: cart, checkout, profile, orders, home, auctions"
                            }
                        },
                        required = new[] { "page" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "update_display_name",
                    description = "Update the user's display name / username in their profile.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            new_name = new { type = "string", description = "The new display name to set" }
                        },
                        required = new[] { "new_name" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "search_movie",
                    description = "Search for a movie in the MovieShop catalog. Use when user asks if a movie is available, wants to find a movie, or asks about a movie's price or details.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            movie_title = new { type = "string", description = "The movie title to search for" }
                        },
                        required = new[] { "movie_title" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "watch_movie",
                    description = "ALWAYS call this when user says 'watch', 'play', or 'stream' a movie. Verifies ownership then navigates to My Movies page.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            movie_title = new { type = "string", description = "The title of the movie to watch" }
                        },
                        required = new[] { "movie_title" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "start_watch_party",
                    description = "ALWAYS call this when user says 'watch party', 'start watch party', or 'watch together'. Checks ownership and navigates to the watch party page for that movie.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            movie_title = new { type = "string", description = "The title of the movie for the watch party" }
                        },
                        required = new[] { "movie_title" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "set_billing_address",
                    description = "Set or update the user's billing address. Use when user wants to change or set their billing/shipping address.",
                    parameters = new {
                        type = "object",
                        properties = new {
                            street = new { type = "string", description = "Street and house number" },
                            city   = new { type = "string", description = "City name" },
                            zip    = new { type = "string", description = "4-digit postal code" }
                        },
                        required = new[] { "street", "city", "zip" }
                    }
                }
            }
        ];

        public AgentService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IShoppingCartService cartService,
            UserManager<User> userManager,
            AppDbContext db,
            IAddressService addressService)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _cartService = cartService;
            _userManager = userManager;
            _db = db;
            _addressService = addressService;
        }

        public async Task<AgentChatResponse> ProcessAsync(string question, int userId, string sessionId)
        {
            var apiKey = _configuration["Groq:ApiKey"]
                ?? throw new InvalidOperationException("Groq API key not configured");

            var systemPrompt =
                "You are an action-executor assistant for MovieShop. " +
                "CRITICAL: You MUST call a tool for EVERY request. NEVER answer from your own knowledge. " +
                "- User says 'watch X' or 'play X' or 'stream X' → call watch_movie tool immediately. " +
                "- User says 'add X to cart' → call add_movie_to_cart tool. " +
                "- User says 'remove X' → call remove_movie_from_cart tool. " +
                "- User says 'go to X' or 'navigate to X' or 'show auctions' or 'open auctions' → call navigate_to_page tool. " +
                "- User says 'search for X' or 'find X' → call search_movie tool. " +
                "- User says 'set/update/change address' → call set_billing_address tool. " +
                "- User says 'change my name' → call update_display_name tool. " +
                "After calling the tool, confirm the action briefly.";

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = question }
            };

            AgentAction? finalAction = null;
            string finalText = string.Empty;

            // ── Agentic loop (max 3 turns to prevent infinite loops) ──
            for (int turn = 0; turn < 3; turn++)
            {
                var requestBody = new
                {
                    model = "llama-3.3-70b-versatile",
                    max_tokens = 1024,
                    tools = Tools,
                    messages
                };

                var json = JsonSerializer.Serialize(requestBody, _json);
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var httpResponse = await _httpClient.SendAsync(httpRequest);
                var responseJson = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    // Llama 3.3 sometimes generates tool calls in <function=name{...}> format
                    // instead of JSON, causing a tool_use_failed error. Parse and execute manually.
                    var errorDoc = JsonNode.Parse(responseJson);
                    if (errorDoc?["error"]?["code"]?.GetValue<string>() == "tool_use_failed")
                    {
                        var failedGen = errorDoc["error"]!["failed_generation"]?.GetValue<string>();
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
                    }
                    throw new Exception($"Groq API error: {responseJson}");
                }

                var doc = JsonNode.Parse(responseJson)!;
                var choice = doc["choices"]![0]!;
                var message = choice["message"]!;
                var finishReason = choice["finish_reason"]?.GetValue<string>();

                // Collect text from this turn
                var content = message["content"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(content))
                    finalText = content;

                // If model is done (no tool calls), exit loop
                if (finishReason != "tool_calls") break;

                // ── Execute tool calls ────────────────────────────────
                var toolCalls = message["tool_calls"]?.AsArray();
                if (toolCalls == null || toolCalls.Count == 0) break;

                // Add assistant message with tool_calls to history
                messages.Add(new
                {
                    role = "assistant",
                    content = message["content"]?.GetValue<string>() ?? (object?)null,
                    tool_calls = toolCalls.Deserialize<object[]>(_json)
                });

                // Execute each tool and add results
                foreach (var toolCall in toolCalls)
                {
                    var toolCallId = toolCall!["id"]!.GetValue<string>();
                    var toolName = toolCall["function"]!["name"]!.GetValue<string>();
                    var argsJson = toolCall["function"]!["arguments"]!.GetValue<string>();
                    var input = JsonNode.Parse(argsJson)!.AsObject();

                    var (resultText, action) = await ExecuteToolAsync(toolName, input, userId);
                    if (action != null) finalAction = action;

                    messages.Add(new
                    {
                        role = "tool",
                        tool_call_id = toolCallId,
                        content = resultText
                    });
                }
            }

            return new AgentChatResponse
            {
                Answer = string.IsNullOrWhiteSpace(finalText) ? "Done! Let me know if you need anything else." : finalText,
                Source = "agent",
                Action = finalAction
            };
        }

        // ── Tool execution ────────────────────────────────────────────
        private async Task<(string result, AgentAction? action)> ExecuteToolAsync(
            string toolName, JsonObject input, int userId)
        {
            return toolName switch
            {
                "add_movie_to_cart"      => await AddToCartAsync(input, userId),
                "remove_movie_from_cart" => await RemoveFromCartAsync(input, userId),
                "navigate_to_page"       => ExecuteNavigate(input),
                "update_display_name"    => await UpdateDisplayNameAsync(input, userId),
                "search_movie"           => await SearchMovieAsync(input),
                "watch_movie"            => await WatchMovieAsync(input, userId),
                "start_watch_party"      => await StartWatchPartyAsync(input, userId),
                "set_billing_address"    => await SetBillingAddressAsync(input, userId),
                _                        => ("Unknown tool", null)
            };
        }


        private async Task<(string, AgentAction?)> AddToCartAsync(JsonObject input, int userId)
        {
            var title = input["movie_title"]?.GetValue<string>() ?? "";
            var movie = await _db.Movies
                .Where(m => !m.IsDeleted && m.Title.ToLower().Contains(title.ToLower()))
                .FirstOrDefaultAsync();

            if (movie == null)
                return ($"Movie '{title}' not found in our catalog.", null);

            var success = await _cartService.AddToCartAsync(userId, movie.Id, 1);
            if (!success)
                return ($"Could not add '{movie.Title}' to cart (you may already own it or it's already in your cart).", null);

            var action = new AgentAction
            {
                Type = "cart_updated",
                Payload = new() { ["movieTitle"] = movie.Title, ["movieId"] = movie.Id }
            };
            return ($"Successfully added '{movie.Title}' to your cart.", action);
        }

        private async Task<(string, AgentAction?)> RemoveFromCartAsync(JsonObject input, int userId)
        {
            var title = input["movie_title"]?.GetValue<string>() ?? "";
            var movie = await _db.Movies
                .Where(m => m.Title.ToLower().Contains(title.ToLower()))
                .FirstOrDefaultAsync();

            if (movie == null)
                return ($"Movie '{title}' not found.", null);

            var success = await _cartService.RemoveFromCartAsync(userId, movie.Id);
            if (!success)
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

        private async Task<(string, AgentAction?)> UpdateDisplayNameAsync(JsonObject input, int userId)
        {
            var newName = input["new_name"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(newName))
                return ("Name cannot be empty.", null);

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return ("User not found.", null);

            user.UserName = newName;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return ($"Failed to update name: {string.Join(", ", result.Errors.Select(e => e.Description))}", null);

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
            var movies = await _db.Movies
                .Include(m => m.Categories)
                .Where(m => !m.IsDeleted && m.Title.ToLower().Contains(title.ToLower()))
                .Take(3)
                .ToListAsync();

            if (movies.Count == 0)
                return ($"No movie found matching '{title}' in our catalog.", null);

            var sb = new System.Text.StringBuilder();
            foreach (var m in movies)
            {
                var price = m.DiscountedPrice.HasValue
                    ? $"{m.DiscountedPrice} Ft (discounted from {m.Price} Ft)"
                    : $"{m.Price} Ft";
                var cats = string.Join(", ", m.Categories?.Select(c => c.Name) ?? []);
                sb.AppendLine($"- {m.Title} | {price} | Categories: {cats}");
            }

            return ($"Found in our catalog:\n{sb}", null);
        }

        private async Task<(string, AgentAction?)> WatchMovieAsync(JsonObject input, int userId)
        {
            var title = input["movie_title"]?.GetValue<string>() ?? "";

            var movie = await _db.Movies
                .Where(m => !m.IsDeleted && m.Title.ToLower().Contains(title.ToLower()))
                .FirstOrDefaultAsync();

            if (movie == null)
                return ($"Movie '{title}' not found in our catalog.", null);

            var owned = await _db.OrderMovies
                .Include(om => om.Order)
                .AnyAsync(om => om.Order.UserId == userId && om.MovieId == movie.Id);

            if (!owned)
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
            var movie = await _db.Movies
                .Where(m => !m.IsDeleted && m.Title.ToLower().Contains(title.ToLower()))
                .FirstOrDefaultAsync();

            if (movie == null)
                return ($"Movie '{title}' not found in our catalog.", null);

            var owned = await _db.OrderMovies
                .Include(om => om.Order)
                .AnyAsync(om => om.Order.UserId == userId && om.MovieId == movie.Id);

            if (!owned)
                return ($"You don't own '{movie.Title}' yet. Purchase it first to start a watch party!", null);

            var action = new AgentAction
            {
                Type = "navigate",
                Payload = new() { ["path"] = $"/my-movies/{movie.Id}/watch-party" }
            };
            return ($"Starting a watch party for '{movie.Title}'! Invite your friends.", action);
        }

        // Fallback for Llama 3.3's <function=name{args}></function> format
        private async Task<(string text, AgentAction? action)?> TryFallbackExecutionAsync(string failedGeneration, int userId)
        {
            var match = Regex.Match(failedGeneration, @"<function=(\w+)(\{.*?\})", RegexOptions.Singleline);
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

        private async Task<(string, AgentAction?)> SetBillingAddressAsync(JsonObject input, int userId)
        {
            var street = input["street"]?.GetValue<string>() ?? "";
            var city   = input["city"]?.GetValue<string>() ?? "";
            var zip    = input["zip"]?.GetValue<string>() ?? "";

            if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(zip))
                return ("Please provide street, city, and zip code.", null);

            var addressDto = new AddressDto { Street = street, City = city, Zip = zip };

            var navigateAction = new AgentAction
            {
                Type = "navigate",
                Payload = new() { ["page"] = "cart" }
            };

            var existing = await _addressService.GetUserAddressesAsync(userId);
            if (existing.Count > 0)
            {
                await _addressService.UpdateAddressAsync(existing[0].Id, addressDto, userId);
                return ($"Your billing address has been updated to: {street}, {city} {zip}. Taking you to your cart.", navigateAction);
            }
            else
            {
                await _addressService.CreateAddressAsync(addressDto, userId);
                return ($"Your billing address has been set to: {street}, {city} {zip}. Taking you to your cart.", navigateAction);
            }
        }
    }
}
