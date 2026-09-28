using System.ComponentModel.DataAnnotations;

namespace MovieShop.OrderService.Models;

public enum OrderStatus
{
    Pending,    // fizetésre vár
    Completed,  // sikeres fizetés, a tartalom elérhető
    Failed,     // sikertelen fizetés
    Cancelled,  // a felhasználó vagy az admin lemondta
    Refunded    // visszatérítve
}

/// <summary>
/// A monolit Order modelljéhez képest két lényeges változás történt.
///
/// 1. A számlázási cím IDEGEN KULCS helyett PILLANATKÉP. A cím a User Service
///    adatbázisában él, így FK nem is lehetne rá. De ez üzletileg is helyesebb:
///    ha a felhasználó jövőre átírja a címét, attól a mostani számlán szereplő
///    cím nem változhat visszamenőleg. Az AddressId csak nyomon követésre marad,
///    kényszer nélküli egész számként.
///
/// 2. A `User` navigációs property megszűnt. A felhasználó neve és e-mail címe
///    az admin nézetekhez a lokális UserSnapshot projekcióból jön.
/// </summary>
public class Order
{
    [Key]
    public int Id { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public int TotalPrice { get; set; }

    public int UserId { get; set; }

    public List<OrderMovie> OrderMovies { get; set; } = [];

    // ── Számlázási cím pillanatképe ──────────────────────────────────────────
    /// <summary>A User Service címazonosítója. Csak hivatkozás, nem idegen kulcs.</summary>
    public int? BillingAddressId { get; set; }

    [Required]
    public string BillingStreet { get; set; } = string.Empty;

    [Required]
    public string BillingCity { get; set; } = string.Empty;

    [Required]
    public string BillingZip { get; set; } = string.Empty;

    public int? ShippingAddressId { get; set; }

    public string Status { get; set; } = OrderStatus.Pending.ToString();

    /// <summary>A Stripe PaymentIntent azonosítója — a fizetés utólagos ellenőrzéséhez.</summary>
    public string? PaymentIntentId { get; set; }
}

/// <summary>
/// A `Movie` navigációs property helyett csak MovieId marad; a film címét és árát
/// a lokális MovieSnapshot táblából olvassuk. A PriceAtOrder továbbra is rögzíti a
/// vásárláskori árat — ez független a snapshottól, mert a snapshot változhat.
/// </summary>
public class OrderMovie
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int MovieId { get; set; }

    public int Quantity { get; set; } = 1;
    public int PriceAtOrder { get; set; }
}

public class ShoppingCart
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    public List<ShoppingCartMovie> ShoppingCartMovies { get; set; } = [];
}

public class ShoppingCartMovie
{
    public int ShoppingCartId { get; set; }
    public ShoppingCart ShoppingCart { get; set; } = null!;

    public int MovieId { get; set; }

    public int Quantity { get; set; } = 1;
    public int PriceAtOrder { get; set; }
}
