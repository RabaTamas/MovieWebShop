namespace MovieShop.Server.DTOs
{
    public class MovieDetailsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Price { get; set; }
        public int? DiscountedPrice { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public List<CategoryDto>? Categories { get; set; }
        public List<ReviewDto>? Reviews { get; set; }

        public string? VideoFileName { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
