﻿namespace MovieShop.Models
{
    public class OrderMovie
    {
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;

        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        public int Quantity { get; set; } = 1;

        public decimal PriceAtOrder { get; set; }

    }
}
