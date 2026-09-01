namespace MovieShop.Server.DTOs
{
    public class RecommendationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
        public decimal? DiscountedPrice { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> Categories { get; set; } = [];
        public string Reason { get; set; } = "";
        public double Score { get; set; }
    }

    public class RecommendationsResultDto
    {
        public List<RecommendationDto> CategoryBased { get; set; } = [];
        public List<RecommendationDto> CollaborativeBased { get; set; } = [];
    }
}
