using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;
using System.Collections.Concurrent;

namespace MovieShop.Server.Services.Implementations
{
    public class ConversationService : IConversationService
    {
        private static readonly ConcurrentDictionary<string, ConversationHistory> _conversations = new();

        public void AddMessage(string sessionId, string role, string content)
        {
            var conversation = _conversations.GetOrAdd(sessionId, _ => new ConversationHistory
            {
                SessionId = sessionId
            });

            conversation.Messages.Add(new ConversationMessage
            {
                Role = role,
                Content = content,
                Timestamp = DateTime.UtcNow
            });

            conversation.LastActivity = DateTime.UtcNow;

            // Limit to last 20 messages
            if (conversation.Messages.Count > 20)
            {
                conversation.Messages = conversation.Messages.TakeLast(20).ToList();
            }
        }

        public List<ConversationMessage> GetHistory(string sessionId, int maxMessages = 10)
        {
            if (_conversations.TryGetValue(sessionId, out var conversation))
            {
                return conversation.Messages.TakeLast(maxMessages).ToList();
            }

            return new List<ConversationMessage>();
        }

        public void ClearHistory(string sessionId)
        {
            _conversations.TryRemove(sessionId, out _);
        }

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
}
