using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.Models
{
    public class ShoppingCart
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public List<ShoppingCartMovie>? ShoppingCartMovies { get; set; }
    }
}
