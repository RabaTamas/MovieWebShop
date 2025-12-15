namespace MovieShop.Server.Services.Interfaces
{
    public interface IChatService
    {
        Task<string> GetContextualAnswer(string question, string sessionId, string? userId = null);
        Task<string> GetUserContext(string? userId);
    }
}
