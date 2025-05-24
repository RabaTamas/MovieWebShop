namespace MovieShop.Server.DTOs
{
    public class ReviewDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public MovieListDto? Movie { get; set; }
        public UserDto? User { get; set; }

        public int UserId { get; set; }
    }
}
