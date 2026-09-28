using System.ComponentModel.DataAnnotations;

namespace MovieShop.CatalogService.Models;

/// <summary>
/// Jogosultság-projekció: ki melyik filmet nézheti.
///
/// A monolitban erre nem volt külön tábla — a `MovieController` minden egyes HLS
/// playlist-kérésnél végigfutott a rendeléseken:
///     Orders.Where(o =&gt; o.UserId == x &amp;&amp; o.Status == "Completed")
///           .AnyAsync(o =&gt; o.OrderMovies.Any(om =&gt; om.MovieId == y))
///
/// Mikroszervizekben ez a lekérdezés az Order Service adatbázisában lenne, vagyis
/// minden minőségváltásnál hálózati hívás indulna lejátszás közben. Ehelyett a
/// Catalog az OrderCompleted és AuctionPaid eseményekből előre felépíti ezt a
/// táblát, így az ellenőrzés egyetlen indexelt, lokális olvasás marad.
///
/// Ez a CQRS olvasási modell (read model) mintája: az írás máshol történik,
/// az olvasás ott, ahol szükség van rá.
/// </summary>
public class Entitlement
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }
    public int MovieId { get; set; }

    /// <summary>Honnan származik a jogosultság: "Order" vagy "Auction".</summary>
    public string Source { get; set; } = "Order";

    /// <summary>A forrás rendelés vagy aukció azonosítója — visszavonáshoz kell.</summary>
    public int SourceId { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}
