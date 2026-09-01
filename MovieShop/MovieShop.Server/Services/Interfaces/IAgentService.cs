using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IAgentService
    {
        Task<AgentChatResponse> ProcessAsync(string question, int userId, string sessionId);
    }
}
