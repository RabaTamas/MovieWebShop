using System.ComponentModel.DataAnnotations;

namespace MovieShop.CatalogService.Models;

/// <summary>
/// A lejátszási pozíció a Catalog Service-ben marad, mert szorosan a streaminghez
/// tartozik: ugyanaz a kérés tölti be, amelyik a videó URL-jét kéri. Külön service-be
/// emelve minden lejátszásindítás egy plusz hálózati ugrás lenne.
///
/// A User navigációs property megszűnt — a UserId önmagában elég, idegen kulcs nélkül.
/// </summary>
public class VideoProgress
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int MovieId { get; set; }

    /// <summary>Másodpercben tárolt lejátszási pozíció.</summary>
    [Required]
    public double ProgressSeconds { get; set; }

    public DateTime LastWatched { get; set; } = DateTime.UtcNow;
}
