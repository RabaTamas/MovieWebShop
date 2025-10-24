using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskQuestion([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { error = "Question cannot be empty" });

            // Session ID from request (frontend generates this)
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

            // FAQ először
            var faqAnswer = CheckFAQ(request.Question);
            if (faqAnswer != null)
            {
                return Ok(new { answer = faqAnswer, source = "FAQ", sessionId });
            }

            // Contextual AI response (with conversation history)
            var aiAnswer = await _chatService.GetContextualAnswer(request.Question, sessionId);
            return Ok(new { answer = aiAnswer, source = "AI", sessionId });
        }

        //public async Task<IActionResult> AskQuestion([FromBody] ChatRequest request)
        //{
        //    if (string.IsNullOrWhiteSpace(request.Question))
        //        return BadRequest(new { error = "Question cannot be empty" });

        //    // FAQ először
        //    var faqAnswer = CheckFAQ(request.Question);
        //    if (faqAnswer != null)
        //    {
        //        return Ok(new { answer = faqAnswer, source = "FAQ" });
        //    }

        //    // Contextual AI response (with database integration)
        //    var aiAnswer = await _chatService.GetContextualAnswer(request.Question);
        //    return Ok(new { answer = aiAnswer, source = "AI" });
        //}



        private string? CheckFAQ(string question)
        {
            var faqs = new Dictionary<string, string>
            {
                { "payment method", "We accept Stripe card payments (Visa, Mastercard, American Express)." },
                { "how to pay", "You can pay with Stripe card payment." },
                { "shipping time", "Shipping usually takes 3-5 business days." },
                { "when arrive", "Your order will arrive in 3-5 business days." },
                { "can return", "Yes, you can return unopened items within 14 days." },
                { "contact", "Email: support@movieshop.com, Phone: +36 1 234 5678" }
            };

            var lowerQuestion = question.ToLower();
            foreach (var faq in faqs)
            {
                if (lowerQuestion.Contains(faq.Key))
                    return faq.Value;
            }

            return null;
        }
    }

    public class ChatRequest
    {
        public string Question { get; set; } = string.Empty;

        public string? SessionId { get; set; }
    }
}