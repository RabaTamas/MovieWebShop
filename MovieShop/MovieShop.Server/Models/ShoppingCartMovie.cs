namespace MovieShop.Server.Models
{
    public class ShoppingCartMovie
    {
        public int ShoppingCartId { get; set; }
        public ShoppingCart ShoppingCart { get; set; } = null!;

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public int Quantity { get; set; } = 1;

        public int PriceAtOrder { get; set; }
    }
}
