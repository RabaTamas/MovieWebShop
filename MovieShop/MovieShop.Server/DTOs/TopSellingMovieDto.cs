namespace MovieShop.Server.DTOs
{
    public class TopSellingMovieDto
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int Revenue { get; set; }
    }
}
