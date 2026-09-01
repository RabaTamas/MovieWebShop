namespace MovieShop.Server.DTOs
{
    public class SearchRequestDto
    {
        public string? Q { get; set; }
        public string[]? Categories { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public double? MinRating { get; set; }
        public string? Sort { get; set; }   // price-asc | price-desc | name-asc | name-desc | rating-desc
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 8;
    }

    public class SearchResultDto
    {
        public IEnumerable<MovieSearchItemDto> Movies { get; set; } = [];
        public long Total { get; set; }
        public int Page { get; set; }
        public int Size { get; set; }
    }

    public class MovieSearchItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string[] Categories { get; set; } = [];
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }
}
