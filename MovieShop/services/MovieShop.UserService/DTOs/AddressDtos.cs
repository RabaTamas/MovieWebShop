using System.ComponentModel.DataAnnotations;

namespace MovieShop.UserService.DTOs;

public class AddressDto
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Street { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string City { get; set; } = string.Empty;

    [RegularExpression(@"^\d{4}$", ErrorMessage = "The postal code must consist of exactly 4 digits.")]
    public string Zip { get; set; } = string.Empty;
}

public class AdminAddressDto
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Zip { get; set; } = string.Empty;

    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// A cím felhasználása rendelésekben. Ez az adat az Order Service-ben él, ezért
    /// szinkron REST-hívással érkezik. Ha az Order Service nem elérhető, 0 marad —
    /// az admin lista ettől még betölt (graceful degradation).
    /// </summary>
    public int BillingOrdersCount { get; set; }
    public int ShippingOrdersCount { get; set; }
}
