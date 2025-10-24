namespace MovieShop.Server.Services.Interfaces
{
    public interface IChatService
    {
        //Task<string> GetContextualAnswer(string question);
        Task<string> GetContextualAnswer(string question, string sessionId);
    }
}
