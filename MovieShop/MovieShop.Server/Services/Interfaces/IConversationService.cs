using MovieShop.Server.Models;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IConversationService
    {
        void AddMessage(string sessionId, string role, string content);
        List<ConversationMessage> GetHistory(string sessionId, int maxMessages = 10);
        void ClearHistory(string sessionId);
        void CleanupOldSessions(int maxAgeMinutes = 30);
    }
}
