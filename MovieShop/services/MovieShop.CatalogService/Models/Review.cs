using System.ComponentModel.DataAnnotations;

namespace MovieShop.CatalogService.Models;

/// <summary>
/// A monolitban a Review-nak `User` navigációs property-je volt az Identity táblára.
/// Itt csak a `UserId` marad — a szerző neve a lokális UserSnapshot táblából jön,
/// amit a User Service UserChanged eseményei töltenek fel.
///
/// Cserébe egy értékeléslista megjelenítése továbbra is EGY adatbázis-lekérdezés,
/// nem N darab hívás a User Service felé.
/// </summary>
public class Review
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public int UserId { get; set; }

    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
