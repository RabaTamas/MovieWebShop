namespace MovieShop.Server.DTOs
{
    public class OrderStatisticsDto
    {
        public int TotalOrders { get; set; }
        public int TotalRevenue { get; set; }
        public int OrdersToday { get; set; }
        public int RevenueToday { get; set; }
        public Dictionary<string, int> OrdersByStatus { get; set; } = new();
        public List<TopSellingMovieDto> TopSellingMovies { get; set; } = new();
    }
}
