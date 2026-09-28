using System.ComponentModel.DataAnnotations;

namespace MovieShop.UserService.DTOs;

/// <summary>A böngésző PushSubscription.toJSON() kimenete: { endpoint, keys: { p256dh, auth } }.</summary>
public class PushSubscriptionDto
{
    [Required]
    [MaxLength(800)]
    public string Endpoint { get; set; } = string.Empty;

    [Required]
    public PushSubscriptionKeysDto Keys { get; set; } = new();
}

public class PushSubscriptionKeysDto
{
    [Required]
    [MaxLength(200)]
    public string P256dh { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Auth { get; set; } = string.Empty;
}

public class PushUnsubscribeDto
{
    [Required]
    public string Endpoint { get; set; } = string.Empty;
}

/// <summary>Az értesítés tartalma — a frontend Service Workere (src/sw.js) ebből jeleníti meg.</summary>
public class PushMessageDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Image { get; set; }
    public string Url { get; set; } = "/";
    public string? Tag { get; set; }
}

public class PushSendResultDto
{
    public int Sent { get; set; }
    public int Removed { get; set; }
    public int Failed { get; set; }
}
