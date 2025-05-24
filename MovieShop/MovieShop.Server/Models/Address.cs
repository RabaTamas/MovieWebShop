using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.Models
{
    public class Address
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Street { get; set; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string City { get; set; }
        [Required]
        [RegularExpression(@"^\d{4}$", ErrorMessage = "The postal code must consist of exactly 4 digits.")]
        public string Zip { get; set; } = null!;


        public int UserId { get; set; }
        public User? User { get; set; }

        public List<Order> BillingOrders { get; set; } = new();
        public List<Order> ShippingOrders { get; set; } = new();

    }
}
