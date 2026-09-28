using System.ComponentModel.DataAnnotations;

namespace MovieShop.UserService.Models;

/// <summary>
/// Egy böngésző (eszköz) Web Push feliratkozása — a monolit azonos nevű entitásának megfelelője.
/// Azért a User Service-ben él, mert a feliratkozás a felhasználóhoz tartozik, és a felhasználó
/// törlésekor kaszkádolva törlődnie kell.
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
