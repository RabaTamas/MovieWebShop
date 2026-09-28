using System.ComponentModel.DataAnnotations;

namespace MovieShop.OrderService.DTOs;

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

public class OrderMovieDto
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public int PriceAtOrder { get; set; }
}

public class OrderDto
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public int TotalPrice { get; set; }

    public AddressDto? BillingAddress { get; set; }
    public List<OrderMovieDto> Movies { get; set; } = [];

    public string Status { get; set; } = string.Empty;

    /// <summary>A lokális UserSnapshot projekcióból — admin nézetekhez.</summary>
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
}

public class OrderRequestDto
{
    public AddressDto BillingAddress { get; set; } = null!;
    public List<OrderMovieDto> Movies { get; set; } = [];
    public int TotalPrice { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public string? PaymentIntentId { get; set; }
}

public class UpdateOrderStatusDto
{
    public string Status { get; set; } = string.Empty;
}

public class ShoppingCartDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public List<ShoppingCartMovieDto> Items { get; set; } = [];
}

public class ShoppingCartMovieDto
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int PriceAtOrder { get; set; }
}

public class AddToCartDto
{
    public int MovieId { get; set; }
    public int Quantity { get; set; }
}

public class TopSellingMovieDto
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Revenue { get; set; }
}

public class OrderStatisticsDto
{
    public int TotalOrders { get; set; }
    public int TotalRevenue { get; set; }
    public int OrdersToday { get; set; }
    public int RevenueToday { get; set; }
    public Dictionary<string, int> OrdersByStatus { get; set; } = [];
    public List<TopSellingMovieDto> TopSellingMovies { get; set; } = [];
}

// ── Ajánlórendszer ───────────────────────────────────────────────────────────

public class RecommendationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public string? ImageUrl { get; set; }
    public List<string> Categories { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class RecommendationsResultDto
{
    public List<RecommendationDto> CategoryBased { get; set; } = [];
    public List<RecommendationDto> CollaborativeBased { get; set; } = [];
}

// ── Fizetés ──────────────────────────────────────────────────────────────────

public class CreatePaymentIntentRequest
{
    public decimal Amount { get; set; }
}

public class VerifyPaymentRequest
{
    public string PaymentIntentId { get; set; } = string.Empty;
}

/// <summary>A User Service admin cím-képernyőjének válaszformátuma.</summary>
public record AddressUsageDto(int AddressId, int BillingCount, int ShippingCount);
