using System.Collections.Concurrent;
using MovieShop.ChatService.Models;

namespace MovieShop.ChatService.Services;

public interface IConversationService
{
    void AddMessage(string sessionId, string role, string content);
    List<ConversationMessage> GetHistory(string sessionId, int maxMessages = 10);
    void ClearHistory(string sessionId);
    void CleanupOldSessions(int maxAgeMinutes = 30);
}

/// <summary>
/// Beszélgetés-előzmények memóriában. A monolitból lényegében változatlanul
/// átvett osztály — a statikus mező helyett viszont példányszintű a tároló,
/// mert singletonként van regisztrálva, és így tesztelhető is.
/// </summary>
public class ConversationService : IConversationService
{
    private const int MaxMessagesPerSession = 20;

    private readonly ConcurrentDictionary<string, ConversationHistory> _conversations = new();

    public void AddMessage(string sessionId, string role, string content)
    {
        var conversation = _conversations.GetOrAdd(sessionId, _ => new ConversationHistory
        {
            SessionId = sessionId
        });

        lock (conversation)
        {
            conversation.Messages.Add(new ConversationMessage
            {
                Role = role,
                Content = content,
                Timestamp = DateTime.UtcNow
            });

            conversation.LastActivity = DateTime.UtcNow;

            if (conversation.Messages.Count > MaxMessagesPerSession)
                conversation.Messages = conversation.Messages.TakeLast(MaxMessagesPerSession).ToList();
        }
    }

    public List<ConversationMessage> GetHistory(string sessionId, int maxMessages = 10)
    {
        if (!_conversations.TryGetValue(sessionId, out var conversation))
            return [];

        lock (conversation)
        {
            return conversation.Messages.TakeLast(maxMessages).ToList();
        }
    }

    public void ClearHistory(string sessionId) => _conversations.TryRemove(sessionId, out _);

    public void CleanupOldSessions(int maxAgeMinutes = 30)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-maxAgeMinutes);

        var oldSessions = _conversations
            .Where(kvp => kvp.Value.LastActivity < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in oldSessions)
            _conversations.TryRemove(sessionId, out _);
    }
}

/// <summary>
/// A monolitban a takarítás csak akkor futott, ha valaki meghívta. Itt önálló
/// háttérszolgáltatás végzi 10 percenként — így az elhagyott munkamenetek
/// biztosan felszabadulnak, forgalomtól függetlenül.
/// </summary>
public class ConversationCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly IConversationService _conversations;
    private readonly ILogger<ConversationCleanupService> _logger;

    public ConversationCleanupService(
        IConversationService conversations,
        ILogger<ConversationCleanupService> logger)
    {
        _conversations = conversations;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                _conversations.CleanupOldSessions();
                _logger.LogDebug("Elavult chat munkamenetek törölve");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hiba a munkamenetek takarításakor");
            }
        }
    }
}
