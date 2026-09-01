namespace MovieShop.Server.DTOs
{
    public class AgentChatResponse
    {
        public string Answer { get; set; } = string.Empty;
        public string Source { get; set; } = "AI";
        public AgentAction? Action { get; set; }
    }

    public class AgentAction
    {
        // Types: "navigate" | "cart_updated" | "profile_updated"
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object> Payload { get; set; } = [];
    }
}
