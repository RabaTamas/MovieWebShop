using System.ComponentModel.DataAnnotations;

namespace MovieShop.Models
{
    public class Address
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Street { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;
        [Required]
        public string Zip { get; set; } = string.Empty;


        public int UserId { get; set; }
        public User? User { get; set; }

        public int? BillingOrderId { get; set; }
        public Order? BillingOrder { get; set; }

        public int? ShippingOrderId { get; set; }
        public Order? ShippingOrder { get; set; }

    }
}
