using System.ComponentModel.DataAnnotations;

namespace MovieShop.UserService.Models;

/// <summary>
/// A monolitban az Address-nek BillingOrders és ShippingOrders navigációs listája volt
/// az Orders táblára. Itt ezek megszűntek: a rendelés a saját adatbázisában, pillanatkép
/// formájában tárolja a számlázási címet (utca/város/irányítószám másolat), nem idegen
/// kulccsal hivatkozik rá.
///
/// Ez üzletileg is helyesebb: ha a felhasználó később átírja a címét, attól a két éve
/// kiállított számlán szereplő cím nem változhat meg visszamenőleg.
/// </summary>
public class Address
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Street { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string City { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "The postal code must consist of exactly 4 digits.")]
    public string Zip { get; set; } = string.Empty;

    public int UserId { get; set; }
    public User? User { get; set; }
}
