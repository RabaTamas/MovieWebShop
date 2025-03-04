using System.ComponentModel.DataAnnotations;

namespace MovieShop.Models
{
    public class ShoppingCart
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public List<ShoppingCartMovie>? ShoppingCartMovies { get; set; }
    }
}
