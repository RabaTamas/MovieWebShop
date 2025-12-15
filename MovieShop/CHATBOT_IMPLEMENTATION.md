# AI Chatbot Megvalósítás - MovieShop Alkalmazás

## Áttekintés
A MovieShop AI chatbot funkció **Groq API (Llama 3.1)** modellt használ intelligens filmajánlásokhoz és ügyfélszolgálati támogatáshoz. A chatbot kontextus-tudatos, személyre szabott válaszokat ad a felhasználó vásárlási előzményei, bevásárlókosara és megrendeléseinek adatai alapján.

**Főbb jellemzők**:
- **Groq API (Llama 3.1-8b-instant)**: Ultragyors AI válaszok
- **In-memory conversation history**: ConversationService (ConcurrentDictionary)
- **Kontextus-alapú válaszok**: User cart, orders, purchased movies
- **FAQ + AI hybrid**: Gyakori kérdések gyors válaszai + AI contextual support
- **Anonymous + Authenticated**: Működik bejelentkezés nélkül is
- **Session-based**: Frontend localStorage sessionId kezelés

---

## Architektúra

### **Komponensek**

1. **Backend API** (.NET 8)
   - ChatController: REST endpoint chat üzenetekhez (`/api/Chat/ask`)
   - ChatService: Groq API integráció, context építés, AI hívás
   - ConversationService: In-memory conversation history (ConcurrentDictionary)
   - FAQ logika: Gyors válaszok beépített kérdésekre

2. **Groq API**
   - Model: `llama-3.1-8b-instant` (gyors, hatékony)
   - OpenAI kompatibilis API endpoint
   - Max tokens: 250, Temperature: 0.3 (alacsony hallucináció)

3. **In-Memory Storage**
   - ConversationService: Session-alapú üzenet tárolás (max 20 üzenet/session)
   - Automatic cleanup: 30 perc inaktivitás után session törlés
   - **Nincs adatbázis perzisztálás** - memóriában tárolva

4. **Frontend** (React)
   - Chatbot.jsx: Floating chat widget (bubble + window)
   - LocalStorage sessionId: Conversation continuity
   - FAQ quick buttons: Előre definiált kérdések
   - Optional auth: Token használata ha user bejelentkezett

---

## Backend Implementáció

### **1. In-Memory Conversation Service**

**ConversationService - Session-based message storage:**
```csharp
// ConcurrentDictionary használata thread-safe in-memory tároláshoz
public class ConversationService : IConversationService
{
    // Static dictionary - alkalmazás újraindításig él
    private static readonly ConcurrentDictionary<string, ConversationHistory> _conversations = new();

    // Üzenet hozzáadása session-höz
    public void AddMessage(string sessionId, string role, string content)
    {
        var conversation = _conversations.GetOrAdd(sessionId, _ => new ConversationHistory
        {
            SessionId = sessionId
        });

        conversation.Messages.Add(new ConversationMessage
        {
            Role = role, // "user" vagy "assistant"
            Content = content,
            Timestamp = DateTime.UtcNow
        });

        conversation.LastActivity = DateTime.UtcNow;

        // Maximum 20 üzenet tárolása session-enként (memória optimalizáció)
        if (conversation.Messages.Count > 20)
        {
            conversation.Messages = conversation.Messages.TakeLast(20).ToList();
        }
    }

    // Conversation history lekérése (utolsó N üzenet)
    public List<ConversationMessage> GetHistory(string sessionId, int maxMessages = 10)
    {
        if (_conversations.TryGetValue(sessionId, out var conversation))
        {
            return conversation.Messages.TakeLast(maxMessages).ToList();
        }

        return new List<ConversationMessage>();
    }

    // Session törlése (user reset vagy admin cleanup)
    public void ClearHistory(string sessionId)
    {
        _conversations.TryRemove(sessionId, out _);
    }

    // Automatikus cleanup régi session-ök számára (30 perc inaktivitás)
    public void CleanupOldSessions(int maxAgeMinutes = 30)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-maxAgeMinutes);
        var oldSessions = _conversations
            .Where(kvp => kvp.Value.LastActivity < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in oldSessions)
        {
            _conversations.TryRemove(sessionId, out _);
        }
    }
}
```

**ConversationHistory és ConversationMessage modellek:**
```csharp
// In-memory conversation tárolásához használt modellek
public class ConversationHistory
{
    public string SessionId { get; set; } = string.Empty;
    public List<ConversationMessage> Messages { get; set; } = new();
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
}

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty; // "user" | "assistant" | "system"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
```

---

### **2. ChatController - REST API Endpoint**

**POST /api/Chat/ask endpoint FAQ + AI hybrid logikával:**
```csharp
// Chat endpoint - FAQ check először, majd AI fallback
[HttpPost("ask")]
public async Task<IActionResult> AskQuestion([FromBody] ChatRequest request)
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return BadRequest(new { error = "Question cannot be empty" });

    // Session ID frontend-től (localStorage)
    var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

    // UserId kinyerése JWT token-ből (opcionális - anonymous is működik)
    var userId = User.Identity?.IsAuthenticated == true 
        ? User.FindFirstValue(ClaimTypes.NameIdentifier) 
        : null;

    // 1. FAQ ellenőrzés - instant válaszok gyakori kérdésekre
    var faqAnswer = CheckFAQ(request.Question);
    if (faqAnswer != null)
    {
        return Ok(new { answer = faqAnswer, source = "FAQ", sessionId });
    }

    // 2. AI contextual response (Groq API + user context)
    var aiAnswer = await _chatService.GetContextualAnswer(request.Question, sessionId, userId);
    return Ok(new { answer = aiAnswer, source = "AI", sessionId });
}
```

**FAQ logika - keyword matching:**
```csharp
// Egyszerű FAQ rendszer keyword alapú matchinggel
private string? CheckFAQ(string question)
{
    var faqs = new Dictionary<string, string>
    {
        { "payment method", "We accept Stripe card payments (Visa, Mastercard, American Express)." },
        { "how to pay", "You can pay with Stripe card payment." },
        { "instant access", "Yes! After purchase, you get instant access to stream your movies." },
        { "how to watch", "Go to 'My Movies' to watch your purchased movies online." },
        { "can download", "Currently we only support online streaming. Downloads are not available." },
        { "refund", "You can request a refund within 14 days if you haven't watched the movie." },
        { "contact", "Email: support@movieshop.com, Phone: +36 1 234 5678" }
    };

    var lowerQuestion = question.ToLower();
    foreach (var faq in faqs)
    {
        if (lowerQuestion.Contains(faq.Key))
            return faq.Value;
    }

    return null; // Nincs FAQ match - AI veszi át
}
```

**ChatRequest DTO:**
```csharp
// Request modell frontend-ről
public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
    public string? SessionId { get; set; } // Frontend localStorage sessionId
}
```

---

### **3. ChatService - Context Building és Groq API**

**GetContextualAnswer - Fő chatbot logika:**
```csharp
// Kontextus-tudatos válasz generálás: user context + conversation history + Groq AI
public async Task<string> GetContextualAnswer(string question, string sessionId, string? userId = null)
{
    var lowerQuestion = question.ToLower();

    // 1. User kontextus építés ha bejelentkezett
    var userContext = string.Empty;
    if (!string.IsNullOrEmpty(userId))
    {
        userContext = await GetUserContext(userId);
    }

    // 2. Conversation history lekérése (utolsó 5 üzenet)
    var history = _conversationService.GetHistory(sessionId, 5);

    // 3. Előző kontextus kinyerése (film címek, témák)
    var previousContext = ExtractContextFromHistory(history);

    // 4. Referencia feloldás ("it", "that" → konkrét film cím)
    if (IsReferencingPreviousContext(lowerQuestion) && !string.IsNullOrEmpty(previousContext))
    {
        question = ReplaceReferences(question, previousContext);
        lowerQuestion = question.ToLower();
    }

    // 5. Specifikus film kérdés
    var movieContext = await GetMovieContext(lowerQuestion);
    if (!string.IsNullOrEmpty(movieContext))
    {
        var combinedContext = CombineContexts(userContext, movieContext);
        var answer = await GetAIResponseWithContext(question, combinedContext, history);
        _conversationService.AddMessage(sessionId, "user", question);
        _conversationService.AddMessage(sessionId, "assistant", answer);
        return answer;
    }

    // 6. "Do you have X movie?" ellenőrzés
    if (lowerQuestion.Contains("do you have") || lowerQuestion.Contains("is there"))
    {
        var potentialTitle = ExtractPotentialMovieTitle(lowerQuestion);
        if (!string.IsNullOrEmpty(potentialTitle))
        {
            var exists = await _context.Movies.AnyAsync(m =>
                m.Title.ToLower().Contains(potentialTitle.ToLower()) && !m.IsDeleted);

            if (!exists)
            {
                var notFoundAnswer = $"Sorry, we don't have '{potentialTitle}' in our current inventory. You can browse our available movies or contact us at support@movieshop.com to suggest additions!";
                _conversationService.AddMessage(sessionId, "user", question);
                _conversationService.AddMessage(sessionId, "assistant", notFoundAnswer);
                return notFoundAnswer;
            }
        }
    }

    // 7. Popular/trending filmek
    if (lowerQuestion.Contains("popular") || lowerQuestion.Contains("trending") || 
        lowerQuestion.Contains("best selling") || lowerQuestion.Contains("top"))
    {
        var popularContext = await GetPopularMoviesContext();
        var combinedContext = CombineContexts(userContext, popularContext);
        var answer = await GetAIResponseWithContext(question, combinedContext, history);
        _conversationService.AddMessage(sessionId, "user", question);
        _conversationService.AddMessage(sessionId, "assistant", answer);
        return answer;
    }

    // 8. Kategória (genre) kérdések
    if (lowerQuestion.Contains("genre") || lowerQuestion.Contains("category") ||
        lowerQuestion.Contains("action") || lowerQuestion.Contains("drama") ||
        lowerQuestion.Contains("comedy") || lowerQuestion.Contains("horror"))
    {
        var categoryContext = await GetCategoryContext(lowerQuestion);
        var combinedContext = CombineContexts(userContext, categoryContext);
        var answer = await GetAIResponseWithContext(question, combinedContext, history);
        _conversationService.AddMessage(sessionId, "user", question);
        _conversationService.AddMessage(sessionId, "assistant", answer);
        return answer;
    }

    // 9. Default - általános kérdés
    var defaultContext = CombineContexts(userContext, "");
    var defaultAnswer = await GetAIResponseWithContext(question, defaultContext, history);
    _conversationService.AddMessage(sessionId, "user", question);
    _conversationService.AddMessage(sessionId, "assistant", defaultAnswer);
    return defaultAnswer;
}
```

**GetUserContext - Személyre szabott kontextus:**
```csharp
// Bejelentkezett user adatainak lekérése: cart, orders, purchased movies
public async Task<string> GetUserContext(string? userId)
{
    if (string.IsNullOrEmpty(userId))
        return string.Empty;

    try
    {
        var context = new StringBuilder();
        context.AppendLine("=== USER PERSONAL DATA ===");

        // UserId string → int konverzió
        if (!int.TryParse(userId, out int userIdInt))
            return string.Empty;

        // User info
        var user = await _context.Users.FindAsync(userIdInt);
        if (user != null)
        {
            context.AppendLine($"User: {user.UserName} ({user.Email})");
        }

        // Shopping cart (aktuális kosár tartalom)
        var cart = await _context.ShoppingCarts
            .Include(sc => sc.ShoppingCartMovies)
                .ThenInclude(i => i.Movie)
            .FirstOrDefaultAsync(sc => sc.UserId == userIdInt);

        if (cart != null && cart.ShoppingCartMovies != null && cart.ShoppingCartMovies.Any())
        {
            context.AppendLine($"\nSHOPPING CART ({cart.ShoppingCartMovies.Count} items):");
            foreach (var item in cart.ShoppingCartMovies)
            {
                var price = item.Movie.DiscountedPrice ?? item.Movie.Price;
                context.AppendLine($"- {item.Movie.Title} (Quantity: {item.Quantity}, Price: {price} Ft each)");
            }
            context.AppendLine($"Cart Total: {cart.ShoppingCartMovies.Sum(i => i.Quantity * (i.Movie.DiscountedPrice ?? i.Movie.Price))} Ft");
        }
        else
        {
            context.AppendLine("\nSHOPPING CART: Empty");
        }

        // Recent orders (utolsó 5 megrendelés)
        var recentOrders = await _context.Orders
            .Include(o => o.OrderMovies)
                .ThenInclude(om => om.Movie)
            .Where(o => o.UserId == userIdInt)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .ToListAsync();

        if (recentOrders.Any())
        {
            context.AppendLine($"\nRECENT ORDERS ({recentOrders.Count} orders):");
            foreach (var order in recentOrders)
            {
                var localOrderDate = order.OrderDate.ToLocalTime();
                context.AppendLine($"- Order #{order.Id} placed on {localOrderDate:yyyy-MM-dd HH:mm}:");
                context.AppendLine($"  Status: {order.Status}");
                context.AppendLine($"  Total: {order.TotalPrice} Ft");
                var movieTitles = string.Join(", ", order.OrderMovies?.Select(om => om.Movie.Title) ?? new List<string>());
                context.AppendLine($"  Movies: {movieTitles}");
            }
        }
        else
        {
            context.AppendLine("\nRECENT ORDERS: No orders yet");
        }

        // Purchased movies összesen (összes vásárolt film)
        var purchasedMovies = await _context.OrderMovies
            .Include(om => om.Movie)
            .Include(om => om.Order)
            .Where(om => om.Order.UserId == userIdInt)
            .Select(om => om.Movie.Title)
            .Distinct()
            .ToListAsync();

        if (purchasedMovies.Any())
        {
            context.AppendLine($"\nALL PURCHASED MOVIES ({purchasedMovies.Count} unique titles):");
            context.AppendLine(string.Join(", ", purchasedMovies));
        }

        return context.ToString();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error getting user context: {ex.Message}");
        return string.Empty;
    }
}
```

**GetMovieContext - Specifikus film adatok:**
```csharp
// Film-specifikus kontextus: leírás, ár, kategóriák, VALÓS reviews adatbázisból
private async Task<string> GetMovieContext(string question)
{
    // Film címek keresése adatbázisban
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

    // VALÓS reviews adatbázisból (ne hallucináljon!)
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
```

**GetAIResponseWithContext - Groq API hívás:**
```csharp
// Groq Llama 3.1 API hívás OpenAI-kompatibilis endpoint-on
private async Task<string> GetAIResponseWithContext(string question, string context, List<ConversationMessage> history)
{
    try
    {
        var apiKey = _configuration["Groq:ApiKey"];
        var url = "https://api.groq.com/openai/v1/chat/completions";

        // System prompt - STRICT rules hallucináció ellen
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

        // Messages array építése conversation history-val
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        // Conversation history hozzáadása (utolsó 3 üzenet)
        foreach (var msg in history.TakeLast(3))
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        // User prompt context-tel
        var userPrompt = string.IsNullOrEmpty(context)
            ? question
            : $"=== VERIFIED DATA FROM DATABASE ===\n{context}\n\n=== USER QUESTION ===\n{question}\n\nRemember: ONLY use the information from VERIFIED DATA above. Do NOT invent anything.";

        messages.Add(new { role = "user", content = userPrompt });

        // Groq API request body
        var requestBody = new
        {
            model = "llama-3.1-8b-instant", // Ultragyors Llama model
            messages = messages.ToArray(),
            max_tokens = 250,
            temperature = 0.3, // Alacsony = kevesebb kreativitás = kevesebb hallucináció
            top_p = 0.9
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json"
        );

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        // Groq API POST request
        var response = await _httpClient.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"❌ Groq API error: {responseBody}");
            return "Sorry, I'm having trouble right now. Email: support@movieshop.com";
        }

        // JSON response parsing
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
```

**Helper metódusok:**
```csharp
// Kontextus kombinálás (user context + specific context)
private string CombineContexts(string userContext, string otherContext)
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
    {
        combined.AppendLine(otherContext);
    }

    return combined.ToString();
}

// Referencia ellenőrzés ("it", "that", "this" → előző filmre utal)
private bool IsReferencingPreviousContext(string question)
{
    var references = new[] { "it", "that", "this", "the movie", "the film", "that one", "this one" };
    return references.Any(r => question.Contains(r, StringComparison.OrdinalIgnoreCase));
}

// Referencia csere (pl. "How much is it?" → "How much is Inception?")
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
```

---

## Frontend Implementáció

### **1. Floating Chat Widget - Chatbot.jsx**

**Chatbot komponens - Lebegő chat buborék és ablak:**
```jsx
// Floating AI chatbot widget - bubble button + expandable chat window
import { useState, useEffect, useRef } from 'react';
import { useAuth } from '../contexts/AuthContext';
import API_BASE_URL from '../config/api';
import './Chatbot.css';

const Chatbot = () => {
    const { token } = useAuth();
    const [isOpen, setIsOpen] = useState(false);
    const [messages, setMessages] = useState([]);
    const [inputMessage, setInputMessage] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const messagesEndRef = useRef(null);

    // LocalStorage session management - megmarad page refresh után is
    const sessionId = (() => {
        let id = localStorage.getItem('chatSessionId');
        if (!id) {
            id = `session_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
            localStorage.setItem('chatSessionId', id);
        }
        return id;
    })();

    // Auto scroll chat aljára amikor új üzenet érkezik
    useEffect(() => {
        if (messagesEndRef.current) {
            messagesEndRef.current.scrollIntoView({ behavior: 'smooth' });
        }
    }, [messages]);

    // FAQ quick buttons - előre definiált gyakori kérdések
    const faqButtons = [
        "What movies do you have?",
        "Show me popular movies",
        "Do you have action movies?",
        "What's in my cart?",
        "Tell me about my orders"
    ];

    // Üzenet küldése backend /api/Chat/ask endpoint-ra
    const sendMessage = async (question) => {
        if (!question.trim()) return;

        const userMessage = question.trim();
        setInputMessage('');
        setIsLoading(true);

        // User üzenet hozzáadása UI-hoz
        setMessages(prev => [...prev, { role: 'user', content: userMessage }]);

        try {
            // POST request Groq chatbot API-ra
            const response = await fetch(`${API_BASE_URL}/api/Chat/ask`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(token && { 'Authorization': `Bearer ${token}` }) // Optional auth
                },
                body: JSON.stringify({
                    question: userMessage,
                    sessionId: sessionId
                })
            });

            if (!response.ok) {
                throw new Error(`API error: ${response.status}`);
            }

            const data = await response.json();
            
            // Assistant válasz hozzáadása UI-hoz
            setMessages(prev => [...prev, {
                role: 'assistant',
                content: data.answer,
                source: data.source // "FAQ" vagy "AI"
            }]);

        } catch (err) {
            console.error('Chat error:', err);
            setMessages(prev => [...prev, {
                role: 'assistant',
                content: 'Sorry, something went wrong. Please try again.',
                source: 'ERROR'
            }]);
        } finally {
            setIsLoading(false);
        }
    };

    // Form submit handler
    const handleSubmit = (e) => {
        e.preventDefault();
        sendMessage(inputMessage);
    };

    // FAQ button click handler
    const handleFaqClick = (question) => {
        sendMessage(question);
    };

    // Floating widget JSX - bubble button + expandable chat window
    return (
        <>
            {/* Floating chat button - mindig látható a jobb alsó sarokban */}
            {!isOpen && (
                <button
                    className="chatbot-bubble"
                    onClick={() => setIsOpen(true)}
                    aria-label="Open chat"
                >
                    <i className="bi bi-chat-dots-fill"></i>
                </button>
            )}

            {/* Chat window - kinyitható/bezárható */}
            {isOpen && (
                <div className="chatbot-window">
                    {/* Header - cím és close gomb */}
                    <div className="chatbot-header">
                        <h5>
                            <i className="bi bi-robot me-2"></i>
                            MovieShop Assistant
                        </h5>
                        <button 
                            className="btn-close btn-close-white" 
                            onClick={() => setIsOpen(false)}
                            aria-label="Close chat"
                        ></button>
                    </div>

                    {/* Messages area - scrollable chat history */}
                    <div className="chatbot-messages">
                        {messages.length === 0 && (
                            <div className="welcome-message">
                                <p>👋 Hi! I'm your MovieShop assistant.</p>
                                <p>Try these quick questions:</p>
                                <div className="faq-buttons">
                                    {faqButtons.map((faq, index) => (
                                        <button
                                            key={index}
                                            className="faq-btn"
                                            onClick={() => handleFaqClick(faq)}
                                        >
                                            {faq}
                                        </button>
                                    ))}
                                </div>
                            </div>
                        )}

                        {messages.map((msg, index) => (
                            <div key={index} className={`message ${msg.role}`}>
                                <div className="message-content">
                                    {msg.content}
                                    {msg.source && (
                                        <span className="message-source">
                                            {msg.source === 'FAQ' ? '📚 FAQ' : '🤖 AI'}
                                        </span>
                                    )}
                                </div>
                            </div>
                        ))}

                        {isLoading && (
                            <div className="message assistant">
                                <div className="typing-indicator">
                                    <span></span>
                                    <span></span>
                                    <span></span>
                                </div>
                            </div>
                        )}

                        <div ref={messagesEndRef} />
                    </div>

                    {/* Input area - üzenet írás és küldés */}
                    <div className="chatbot-input">
                        <form onSubmit={handleSubmit}>
                            <input
                                type="text"
                                placeholder="Ask me anything..."
                                value={inputMessage}
                                onChange={(e) => setInputMessage(e.target.value)}
                                disabled={isLoading}
                            />
                            <button 
                                type="submit" 
                                disabled={isLoading || !inputMessage.trim()}
                            >
                                <i className="bi bi-send-fill"></i>
                            </button>
                        </form>
                    </div>
                </div>
            )}
        </>
    );
};

export default Chatbot;
```

**Főbb funkciók**:
- **Floating bubble**: Jobb alsó sarokban mindig látható chat buborék
- **Expandable window**: Kinyitható/bezárható chat ablak (setIsOpen)
- **LocalStorage session**: `sessionId` megmarad page refresh után is
- **FAQ quick buttons**: Előre definiált gyakori kérdések gyors elérésre
- **Optional auth**: `token && { Authorization: Bearer ${token} }` - működik bejelentkezés nélkül is
- **Message source badge**: 📚 FAQ vagy 🤖 AI jelzés minden üzenetnél
- **Typing indicator**: 3-dot animáció loading state alatt

---

## Konfiguráció és Setup

### **1. Groq API Configuration**

**appsettings.json - Groq API key beállítása:**
```json
{
  "Groq": {
    "ApiKey": "your-groq-api-key-here",
    "Model": "llama-3.1-8b-instant",
    "MaxTokens": 250,
    "Temperature": 0.3
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### **2. Dependency Injection Setup**

**Program.cs - ConversationService regisztrálása:**
```csharp
// Singleton service - shared in-memory storage across all requests
builder.Services.AddSingleton<IConversationService, ConversationService>();

// HttpClient factory for Groq API calls
builder.Services.AddHttpClient<ChatService>();

// Controllers
builder.Services.AddControllers();

// CORS policy frontend számára
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Vite dev server
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
```

**Miért Singleton?**
- ConversationService **statikus ConcurrentDictionary**-t használ
- Session history **minden request között megmarad**
- Egy instance az egész alkalmazás élettartama alatt

### **3. Frontend Integration**

**App.jsx - Chatbot widget hozzáadása:**
```jsx
import Chatbot from './components/Chatbot';

function App() {
    return (
        <BrowserRouter>
            {/* Navbar, Routes, Footer... */}
            
            {/* Floating chatbot - mindig látható minden oldalon */}
            <Chatbot />
        </BrowserRouter>
    );
}
```

---

## Production Considerations

### **In-Memory Storage Trade-offs**

**Előnyök** ✅:
- **Ultragyors**: Nincs database I/O overhead
- **Egyszerű**: Nincs szükség migration/schema management-re
- **Stateless-like**: Session alapú, nem kell user account
- **Low cost**: Nincs extra database tárolási költség

**Hátrányok** ⚠️:
- **Nem perzisztens**: Alkalmazás restart = összes session history elvész
- **Memory limit**: Nagy user load esetén memory problémák
- **No analytics**: Nincs hosszú távú chat history analysis
- **Horizontal scaling**: Session affinity szükséges (sticky sessions)

### **Cleanup Strategy**

**ConversationService - automatikus session törlés:**
```csharp
// 30 perc inaktivitás után session cleanup
public void CleanupOldSessions(int maxInactiveMinutes = 30)
{
    var cutoffTime = DateTime.UtcNow.AddMinutes(-maxInactiveMinutes);
    
    var expiredSessions = _conversations
        .Where(kvp => kvp.Value.LastActivity < cutoffTime)
        .Select(kvp => kvp.Key)
        .ToList();
    
    foreach (var sessionId in expiredSessions)
    {
        _conversations.TryRemove(sessionId, out _);
    }
}
```

**Scheduled cleanup job ajánlott**:
- `BackgroundService` 5 percenként cleanup hívás
- Memory monitoring (max 100MB chat history)
- Logging: hány session lett törölve

### **Horizontal Scaling Considerations**

**Ha több backend instance fut (Kubernetes, Docker Swarm)**:
- **Session affinity** szükséges: ugyanaz a sessionId ugyanarra az instance-re kell menjen
- **Redis alternatíva**: Shared in-memory store több szerver között
- **Database persistence**: Long-term analytics, chat history export

**Ajánlott scaling setup**:
```yaml
# docker-compose.yml - sticky sessions példa
services:
  backend:
    deploy:
      replicas: 3
    labels:
      - "traefik.http.services.backend.loadbalancer.sticky=true"
      - "traefik.http.services.backend.loadbalancer.sticky.cookie.name=movieshop_session"
```

---

## Costs and Limitations

### **Groq API Pricing** (2024)

| Metric | Cost | Monthly Estimate (1000 users) |
|--------|------|-------------------------------|
| Requests | $0.10 / 1M tokens | ~$5-10 (avg 50-100k tokens) |
| Max tokens/request | 250 | Optimized for low cost |
| Response time | < 500ms | Ultra-fast vs OpenAI (2-5s) |

**Cost savings vs OpenAI GPT-4**:
- GPT-4: $0.03/1k input tokens, $0.06/1k output tokens
- Groq Llama 3.1: **~10x cheaper**, **5x faster**

### **In-Memory Storage Limits**

**Memory usage estimate**:
- 1 session = ~20 messages × 200 chars avg = 4KB
- 1000 active sessions = **~4MB memory**
- 10,000 active sessions = **~40MB memory** (acceptable)

**Recommended limits**:
- Max 20 messages/session (implemented ✅)
- 30 min cleanup interval (implemented ✅)
- Alert at 100MB total chat history

---

## Future Improvements

### **Rövid távú (1-2 hét)**
- ✅ FAQ keyword matching (implemented)
- ✅ User context (cart, orders) (implemented)
- ⏳ Chat history export (JSON download button)
- ⏳ Admin dashboard: top chatbot questions analytics

### **Közép távú (1-2 hónap)**
- 🔄 Redis shared storage (horizontal scaling support)
- 🔄 Database persistence (chat analytics, training data)
- 🔄 Multi-language support (Groq supports 8+ languages)
- 🔄 Movie poster thumbnails in AI responses

### **Hosszú távú (3+ hónap)**
- 🚀 Voice input (Web Speech API)
- 🚀 Movie recommendations ML model (collaborative filtering)
- 🚀 Sentiment analysis (customer satisfaction tracking)
- 🚀 A/B testing (FAQ vs AI response quality)

