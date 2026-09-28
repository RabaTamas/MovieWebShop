namespace MovieShop.ChatService.Services;

/// <summary>
/// A nyolc eszköz definíciója OpenAI function calling formátumban.
/// Változatlanul átvéve a monolitból — az eszközök leírása a nyelvi modell
/// felé szóló szerződés, ezt nem érinti, hogy mögötte adatbázis vagy REST-hívás van.
///
/// A VÉGREHAJTÁSUK viszont teljesen átíródott: a monolitban közvetlen
/// DbContext-műveletek voltak, itt REST-hívások a Catalog, Order és User
/// Service felé, a felhasználó tokenjével.
/// </summary>
public static class AgentTools
{
    public static readonly object[] Definitions =
    [
        new
        {
            type = "function",
            function = new
            {
                name = "add_movie_to_cart",
                description = "Add a movie to the user's shopping cart. Use this when the user says things like 'add X to cart', 'I want to buy X', 'put X in my cart'.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        movie_title = new { type = "string", description = "The title of the movie to add to the cart" }
                    },
                    required = new[] { "movie_title" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "remove_movie_from_cart",
                description = "Remove a movie from the user's shopping cart.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        movie_title = new { type = "string", description = "The title of the movie to remove" }
                    },
                    required = new[] { "movie_title" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "navigate_to_page",
                description = "Navigate the user to a page. Use ONLY for: cart, checkout, profile, orders, home, auctions. Do NOT use for my-movies, watch, or watch-party - use watch_movie or start_watch_party tools instead.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        page = new
                        {
                            type = "string",
                            description = "Page to navigate to: cart, checkout, profile, orders, home, auctions"
                        }
                    },
                    required = new[] { "page" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "update_display_name",
                description = "Update the user's display name / username in their profile.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        new_name = new { type = "string", description = "The new display name to set" }
                    },
                    required = new[] { "new_name" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "search_movie",
                description = "Search for a movie in the MovieShop catalog. Use when user asks if a movie is available, wants to find a movie, or asks about a movie's price or details.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        movie_title = new { type = "string", description = "The movie title to search for" }
                    },
                    required = new[] { "movie_title" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "watch_movie",
                description = "ALWAYS call this when user says 'watch', 'play', or 'stream' a movie. Verifies ownership then navigates to My Movies page.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        movie_title = new { type = "string", description = "The title of the movie to watch" }
                    },
                    required = new[] { "movie_title" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "start_watch_party",
                description = "ALWAYS call this when user says 'watch party', 'start watch party', or 'watch together'. Checks ownership and navigates to the watch party page for that movie.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        movie_title = new { type = "string", description = "The title of the movie for the watch party" }
                    },
                    required = new[] { "movie_title" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "set_billing_address",
                description = "Set or update the user's billing address. Use when user wants to change or set their billing/shipping address.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        street = new { type = "string", description = "Street and house number" },
                        city = new { type = "string", description = "City name" },
                        zip = new { type = "string", description = "4-digit postal code" }
                    },
                    required = new[] { "street", "city", "zip" }
                }
            }
        }
    ];

    public const string SystemPrompt =
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
}
