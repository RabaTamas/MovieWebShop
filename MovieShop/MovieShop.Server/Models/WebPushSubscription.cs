using System.ComponentModel.DataAnnotations;

namespace MovieShop.Server.Models
{
    /// <summary>
    /// Egy böngésző (eszköz) Web Push feliratkozása. A böngésző PushManager.subscribe()
    /// hívása adja: az Endpoint a böngésző push-szolgáltatójának címe (pl. FCM), a P256dh
    /// és az Auth kulcsokkal titkosítja a szerver az értesítés tartalmát.
    /// Egy felhasználónak több eszköze is lehet; ugyanaz az Endpoint csak egyszer szerepel.
    /// </summary>
    public class WebPushSubscription
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(800)]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string P256dh { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Auth { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? UserAgent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int UserId { get; set; }
        public User? User { get; set; }
    }
}
