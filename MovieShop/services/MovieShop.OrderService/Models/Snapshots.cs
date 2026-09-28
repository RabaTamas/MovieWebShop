using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieShop.OrderService.Models;

/// <summary>
/// A Catalog Service filmadatainak csak olvasható replikája.
///
/// EZ A SZÉTBONTÁS KULCSA. A monolit ajánlórendszere így nézett ki:
///
///     _db.OrderMovies
///        .Include(om =&gt; om.Order)
///        .Include(om =&gt; om.Movie).ThenInclude(m =&gt; m.Categories)
///        .Where(om =&gt; similarUserIds.Contains(om.Order.UserId) &amp;&amp; !om.Movie.IsDeleted)
///        .GroupBy(...)
///
/// Vagyis egyetlen SQL lekérdezésben join-olta a rendeléseket a filmekkel és a
/// kategóriákkal. Két külön adatbázis között ez nem join-olható. A naiv megoldás
/// (minden jelölt filmre egy REST-hívás a Catalog felé) több tucat hálózati kört
/// jelentene egyetlen ajánláslekérésnél.
///
/// Ehelyett a Catalog MovieChanged eseményeiből itt karbantartunk egy másolatot,
/// így a join lokális marad — a lekérdezés gyakorlatilag változatlan formában
/// átvehető volt a monolitból.
///
/// Az ár: végleges konzisztencia. Egy filmmódosítás után ezredmásodpercekig
/// elavult adat látszhat itt. Ajánlásoknál és kosárnál ez elfogadható;
/// a rendelés véglegesítésekor ezért is rögzítjük külön a PriceAtOrder-t.
/// </summary>
public class MovieSnapshot
{
    /// <summary>
    /// A kulcs a FORRÁS service-től érkezik, nem itt keletkezik — ezért tilos
    /// identity oszlopnak lennie. Enélkül az SQL Server 544-es hibával utasítja
    /// vissza a beszúrást ("Cannot insert explicit value for identity column").
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int MovieId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    public int Price { get; set; }
    public int? DiscountedPrice { get; set; }

    /// <summary>
    /// Kategórianevek pontosvesszővel elválasztva. Relációs normalizálás helyett
    /// azért egyetlen mező, mert ez egy denormalizált olvasási modell — sosem
    /// kérdezünk rá kategóriára önmagában, mindig a filmmel együtt olvassuk.
    /// </summary>
    public string CategoriesCsv { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    public List<string> GetCategories()
        => string.IsNullOrEmpty(CategoriesCsv)
            ? []
            : CategoriesCsv.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
}

/// <summary>
/// Felhasználónév és e-mail az admin rendeléslistákhoz, a User Service hívása nélkül.
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

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
