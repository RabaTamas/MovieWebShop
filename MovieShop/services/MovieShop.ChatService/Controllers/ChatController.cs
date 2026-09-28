using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MovieShop.ChatService.Models;
using MovieShop.ChatService.Services;

namespace MovieShop.ChatService.Controllers;

/// <summary>
/// A monolit ChatController-ének megfelelője: azonos háromlépcsős irányítás
/// (FAQ → agent → kontextusalapú Q&amp;A), azonos válaszformátum és szövegek.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IAgentService _agentService;
    private readonly IGroqClient _groq;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        IAgentService agentService,
        IGroqClient groq,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _agentService = agentService;
        _groq = groq;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<IActionResult> AskQuestion([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "Question cannot be empty" });

        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

        var userIdStr = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        // ── 1. FAQ (mindig először, azonnali) ────────────────────────────────
        var faqAnswer = CheckFAQ(request.Question);
        if (faqAnswer != null)
            return Ok(new { answer = faqAnswer, source = "FAQ", sessionId });

        // ── 2. Agentic mód, ha a kérdés műveletet igényel ────────────────────
        if (_groq.IsConfigured && userIdStr != null && RequiresAgentAction(request.Question))
        {
            try
            {
                var userId = int.Parse(userIdStr);
                var agentResult = await _agentService.ProcessAsync(request.Question, userId, sessionId);
                return Ok(new { answer = agentResult.Answer, source = agentResult.Source, action = agentResult.Action, sessionId });
            }
            catch (Exception ex)
            {
                // Agent hiba esetén visszaesés a kontextusalapú válaszra
                _logger.LogWarning(ex, "[AgentService] Error: {Message}", ex.Message);
            }
        }

        // ── 3. Alapértelmezett: kontextusalapú válasz ────────────────────────
        var aiAnswer = await _chatService.GetContextualAnswer(request.Question, sessionId, userIdStr);
        return Ok(new { answer = aiAnswer, source = "AI", sessionId });
    }

    private static bool RequiresAgentAction(string question)
    {
        var q = question.ToLowerInvariant();

        // Kosárműveletek
        if ((q.Contains("add") || q.Contains("put") || q.Contains("place")) &&
            (q.Contains("cart") || q.Contains("basket") || q.Contains("bag"))) return true;
        if ((q.Contains("remove") || q.Contains("delete") || q.Contains("take out")) &&
            (q.Contains("cart") || q.Contains("from my"))) return true;
        if (q.StartsWith("remove ") && q.Length > 7) return true;
        if (q.Contains("i want to buy") || q.Contains("i'd like to buy") || q.Contains("i would like to buy")) return true;

        // Navigáció
        if (q.StartsWith("go to") || q.Contains("take me to") || q.Contains("navigate to")) return true;
        if (q.Contains("open cart") || q.Contains("show cart") || q.Contains("open checkout")) return true;
        if (q.Contains("proceed to checkout") || q.Contains("checkout")) return true;
        if (q.Contains("my profile") || q.Contains("my orders") || q.Contains("my movies")) return true;

        // Profilmódosítás
        if ((q.Contains("update") || q.Contains("change") || q.Contains("set")) &&
            (q.Contains("my name") || q.Contains("display name") || q.Contains("username"))) return true;

        // Keresés
        if (q.Contains("search for") || q.Contains("find movie") || q.Contains("is there a") ||
            q.Contains("do you have") || q.Contains("looking for")) return true;

        // Lejátszás / Watch Party
        if ((q.Contains("watch") || q.Contains("play") || q.Contains("stream") || q.Contains("start watching")) &&
            q.Length > 10) return true;
        if (q.Contains("watch party") || q.Contains("watch together")) return true;

        // Cím
        if (q.Contains("billing address") || q.Contains("shipping address") ||
            ((q.Contains("set") || q.Contains("update") || q.Contains("change")) && q.Contains("address"))) return true;

        return false;
    }

    private static string? CheckFAQ(string question)
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

        var lower = question.ToLower();
        foreach (var faq in faqs)
            if (lower.Contains(faq.Key)) return faq.Value;

        return null;
    }
}
