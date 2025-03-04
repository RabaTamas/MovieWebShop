using System.ComponentModel.DataAnnotations;

namespace MovieShop.Models
{
    public class Movie
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        [Required]
        public decimal Price { get; set; }

        public decimal? DiscountedPrice { get; set; }

        public List<MovieCategory>? MovieCategories { get; set; }
        public List<OrderMovie>? OrderMovies { get; set; }

        public List<ShoppingCartMovie>? ShoppingCartMovies { get; set; }

        public List<Review>? Reviews { get; set; }
    }
}
