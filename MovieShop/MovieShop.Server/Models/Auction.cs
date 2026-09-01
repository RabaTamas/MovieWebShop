using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieShop.Server.Models
{
    public class Auction
    {
        [Key]
        public int Id { get; set; }

        // Movie association is optional — auctions can be for standalone items (e.g. collectibles)
        public int? MovieId { get; set; }
        public Movie? Movie { get; set; }

        // Used when MovieId is null, or as override when set
        public string? AuctionTitle { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }

        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal StartingPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentPrice { get; set; }

        public int? CurrentBidderId { get; set; }
        public User? CurrentBidder { get; set; }

        public AuctionStatus Status { get; set; } = AuctionStatus.Pending;

        public bool IsPaid { get; set; } = false;

        // Optimistic concurrency token — maps to SQL Server rowversion
        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public List<Bid> Bids { get; set; } = [];
    }

    public enum AuctionStatus
    {
        Pending = 0,
        Active  = 1,
        Ended   = 2
    }
}
