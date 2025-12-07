namespace MovieShop.Server.DTOs
{
    public class StreamingUrlDto
    {
        public string Url { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string MovieTitle { get; set; } = string.Empty;
    }
}
