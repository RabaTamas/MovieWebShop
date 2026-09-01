using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MovieShop.Server.Models
{
    public class Bid
    {
        [Key]
        public int Id { get; set; }

        public int AuctionId { get; set; }
        public Auction Auction { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    }
}
