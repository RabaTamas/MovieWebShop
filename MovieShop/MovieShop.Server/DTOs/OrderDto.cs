namespace MovieShop.Server.DTOs
{
    public class OrderDto
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public int TotalPrice { get; set; }

        public AddressDto BillingAddress { get; set; } = null!;
        public AddressDto ShippingAddress { get; set; } = null!;

        public List<OrderMovieDto> Movies { get; set; } = new();

        public string Status { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
    }
}
