using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.DTOs
{
    public class AdminAddressDto
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Street { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string City { get; set; }

        [RegularExpression(@"^\d{4}$", ErrorMessage = "The postal code must consist of exactly 4 digits.")]
        public string Zip { get; set; } = null!;

        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;

        public int BillingOrdersCount { get; set; }
        public int ShippingOrdersCount { get; set; }
    }
}
