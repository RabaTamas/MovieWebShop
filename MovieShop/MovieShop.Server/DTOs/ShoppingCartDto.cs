namespace MovieShop.Server.DTOs
{
    public class ShoppingCartDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public List<ShoppingCartMovieDto> Items { get; set; } = new();
    }
}
