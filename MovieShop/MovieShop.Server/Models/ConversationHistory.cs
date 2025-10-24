namespace MovieShop.Server.Models
{
    public class ConversationHistory
    {
        public string SessionId { get; set; } = string.Empty;
        public List<ConversationMessage> Messages { get; set; } = new();
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }
}
