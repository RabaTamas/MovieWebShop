using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieShop.AuctionService.Models;

public enum AuctionStatus
{
    Pending = 0, // még nem kezdődött el
    Active = 1,  // folyamatban
    Ended = 2    // lezárult
}

/// <summary>
/// Az aukció önálló service-be került, noha a DEVELOPMENT_PLAN táblázata nem
/// említette. Az indok: valós idejű, SignalR-alapú, és a terhelési profilja
/// semmilyen módon nem korrelál a katalógusböngészésével — egy élő aukció
/// utolsó perce percenként több ezer kérést jelenthet, miközben a webshop
/// többi része változatlan terhelésen fut.
///
/// A monolithoz képest a `Movie` és a `CurrentBidder` navigációs property-k
/// eltűntek; helyettük lokális snapshot táblákból olvassuk a film címét és a
/// licitáló nevét.
/// </summary>
public class Auction
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Opcionális filmhivatkozás. NEM idegen kulcs — a film a Catalog Service
    /// adatbázisában él. Null esetén önálló gyűjtői tárgyról van szó.
    /// </summary>
    public int? MovieId { get; set; }

    public string? AuctionTitle { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal StartingPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentPrice { get; set; }

    /// <summary>A legmagasabb ajánlatot tevő felhasználó. Szintén nem idegen kulcs.</summary>
    public int? CurrentBidderId { get; set; }

    public AuctionStatus Status { get; set; } = AuctionStatus.Pending;

    public bool IsPaid { get; set; }

    /// <summary>
    /// Optimista konkurenciavezérlés SQL Server rowversion típussal.
    /// Ez a mező változatlanul átjött a monolitból, és a szétbontás után is
    /// ugyanúgy működik — mert a licitelés egyetlen service egyetlen
    /// adatbázisán belül zajlik. Ha az aukció és a licitek külön service-be
    /// kerültek volna, ide elosztott tranzakció kellett volna.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public List<Bid> Bids { get; set; } = [];
}

public class Bid
{
    [Key]
    public int Id { get; set; }

    public int AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;

    public int UserId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
}

// ── Projekciók más service-ek eseményeiből ──────────────────────────────────

/// <summary>Filmcím és borítókép a filmhez kötött aukciókhoz.</summary>
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
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Licitálók megjelenítendő neve. Enélkül a licitlista minden sorához külön
/// hívás menne a User Service felé — egy 20 elemű listánál 20 hálózati kör,
/// valós idejű licitelés közben.
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
