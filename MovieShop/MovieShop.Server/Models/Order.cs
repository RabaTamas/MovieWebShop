﻿using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public int TotalPrice { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public List<OrderMovie> OrderMovies { get; set; } = new();

        public int BillingAddressId { get; set; }
        public Address BillingAddress { get; set; } = null!;

        public int ShippingAddressId { get; set; }
        public Address ShippingAddress { get; set; } = null!;

        public string Status { get; set; } = OrderStatus.Pending.ToString();
    }
}
