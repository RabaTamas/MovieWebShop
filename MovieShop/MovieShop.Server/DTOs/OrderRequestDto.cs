namespace MovieShop.Server.DTOs
{
    public class OrderRequestDto
    {
        public AddressDto BillingAddress { get; set; } = null!;
        public AddressDto ShippingAddress { get; set; } = null!;
        public List<OrderMovieDto> Movies { get; set; } = new();
        public int TotalPrice { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public string? PaymentIntentId { get; set; }
    }
}
