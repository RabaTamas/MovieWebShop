namespace MovieShop.ChatService.Models;

/// <summary>
/// A chatbot egy munkamenete. Nem adatbázis-entitás: a ConversationService
/// memóriában, ConcurrentDictionary-ben tárolja őket — ugyanúgy, mint a monolitban.
///
/// SKÁLÁZÁSI KORLÁT: több Chat Service példány esetén minden példánynak saját
/// memóriája van, tehát a felhasználó elveszítené a beszélgetés fonalát, ha a
/// következő kérése másik példányra érkezne.
/// </summary>
public class ConversationHistory
{
    public string SessionId { get; set; } = string.Empty;
    public List<ConversationMessage> Messages { get; set; } = [];
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
    public string? SessionId { get; set; }
}

public class AgentChatResponse
{
    public string Answer { get; set; } = string.Empty;
    public string Source { get; set; } = "AI";
    public AgentAction? Action { get; set; }
}

public class AgentAction
{
    /// <summary>"navigate" | "cart_updated" | "profile_updated"</summary>
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = [];
}

// ── Más service-ektől érkező adatok ──────────────────────────────────────────
// A monolitban ezek egyetlen DbContexten keresztül érkeztek; itt REST-hívások
// válaszai. A mezők pontosan azt fedik le, amit a monolit ChatService és
// AgentService felhasznált — se többet, se kevesebbet.

/// <summary>Film a Catalog Service-től, kategóriákkal és értékelésekkel.</summary>
public class CatalogMovie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; }
    public int? DiscountedPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = [];
    public bool HasVideo { get; set; }
    public List<CatalogReview> Reviews { get; set; } = [];
}

public class CatalogReview
{
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class CategoryItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>A felhasználó friss neve és e-mail címe a User Service-től.</summary>
public class UserInfo
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>A felhasználó vásárlási kontextusa az Order Service-től.</summary>
public class UserOrderContext
{
    public List<CartItem> CartItems { get; set; } = [];
    public List<RecentOrder> RecentOrders { get; set; } = [];

    /// <summary>Minden rendelésből (állapottól függetlenül), ahogy a monolit is számolta.</summary>
    public List<string> PurchasedTitles { get; set; } = [];
    public List<int> PurchasedMovieIds { get; set; } = [];

    public string RecommendationContext { get; set; } = string.Empty;
}

public class CartItem
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>A film AKTUÁLIS ára (akciós, ha van) — a monolit is ezt mutatta a chatben.</summary>
    public int Price { get; set; }
}

public class RecentOrder
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalPrice { get; set; }
    public List<string> Movies { get; set; } = [];
}

public class TopMovie
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int Price { get; set; }
}

public class AddressInfo
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;
}
