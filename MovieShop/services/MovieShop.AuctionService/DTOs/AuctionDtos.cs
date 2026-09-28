using MovieShop.AuctionService.Models;

namespace MovieShop.AuctionService.DTOs;

public class AuctionDto
{
    public int Id { get; set; }
    public int? MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public decimal StartingPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MinBidIncrement { get; set; }
    public int? CurrentBidderId { get; set; }
    public string? CurrentBidderName { get; set; }
    public AuctionStatus Status { get; set; }
    public bool IsPaid { get; set; }
    public List<BidDto> RecentBids { get; set; } = [];
}

public class BidDto
{
    public int Id { get; set; }
    public string BidderName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PlacedAt { get; set; }
}

public class CreateAuctionRequest
{
    public int? MovieId { get; set; }
    public string? AuctionTitle { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public decimal StartingPrice { get; set; }
}

public class PlaceBidRequest
{
    public decimal Amount { get; set; }
}

public class ConfirmAuctionPaymentRequest
{
    public string PaymentIntentId { get; set; } = string.Empty;
}

public class BidResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public decimal CurrentPrice { get; set; }
    public DateTime NewEndsAt { get; set; }
    public bool AntiSnipingTriggered { get; set; }
}
