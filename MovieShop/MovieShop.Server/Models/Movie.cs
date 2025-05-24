using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.Models
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
        public int Price { get; set; }

        public int? DiscountedPrice { get; set; }

        public List<Category>? Categories { get; set; }
        public List<OrderMovie>? OrderMovies { get; set; }

        public List<ShoppingCartMovie>? ShoppingCartMovies { get; set; }

        public List<Review>? Reviews { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Timestamps for audit trail
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
