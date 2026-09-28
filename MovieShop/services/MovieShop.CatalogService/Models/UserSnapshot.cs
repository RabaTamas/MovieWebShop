using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieShop.CatalogService.Models;

/// <summary>
/// A User Service adatainak csak olvasható másolata, kizárólag az értékelések
/// szerzőnevének megjelenítéséhez.
///
/// Fontos: ezt a táblát a Catalog Service SOHA nem írja üzleti műveletből —
/// kizárólag a UserChanged esemény kezelője frissíti. Az adat forrása és
/// tulajdonosa változatlanul a User Service marad.
/// </summary>
public class UserSnapshot
{
    /// <summary>
    /// A kulcs a FORRÁS service-től érkezik, nem itt keletkezik — ezért tilos
    /// identity oszlopnak lennie. Enélkül az SQL Server 544-es hibával utasítja
    /// vissza a beszúrást ("Cannot insert explicit value for identity column").
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int UserId { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>Az utolsó feldolgozott esemény ideje — ebből derül ki, ha egy régi üzenet érkezik sorrenden kívül.</summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
