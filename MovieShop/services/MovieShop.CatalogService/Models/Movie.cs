using System.ComponentModel.DataAnnotations;

namespace MovieShop.CatalogService.Models;

/// <summary>
/// A Catalog Service birtokolja a film teljes adatát — ez a rendszerben az egyetlen
/// hiteles Movie rekord. Az Order és az Auction Service csak szűkített másolatot
/// (MovieSnapshot) tart belőle, amit a MovieChanged esemény frissít.
///
/// Az OrderMovies és ShoppingCartMovies navigációs listák eltűntek: azok az adatok
/// az Order Service adatbázisában élnek.
/// </summary>
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

    public List<Category> Categories { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];

    public bool IsDeleted { get; set; }

    /// <summary>Az Azure Blob Storage-ban lévő HLS mesterlejátszási lista fájlneve.</summary>
    public string? VideoFileName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
