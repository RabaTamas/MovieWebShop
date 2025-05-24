namespace MovieShop.Server.DTOs
{
    public class MovieAdminListDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int? DiscountedPrice { get; set; }
        public int Price { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string Status => IsDeleted ? "Deleted" : "Active";
    }
}
