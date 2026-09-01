using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.Models
{
    public class VideoProgress
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int MovieId { get; set; }

        /// <summary>
        /// Másodpercben tárolt lejátszási pozíció
        /// </summary>
        [Required]
        public double ProgressSeconds { get; set; }

        public DateTime LastWatched { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User User { get; set; } = null!;
        public Movie Movie { get; set; } = null!;
    }
}
