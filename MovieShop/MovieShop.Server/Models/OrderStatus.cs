namespace MovieShop.Server.Models
{
    public enum OrderStatus
    {
        Pending,      // Payment pending
        Completed,    // Payment successful, content available
        Failed,       // Payment failed
        Cancelled,    // Order cancelled by user/admin
        Refunded      // Order refunded
    }
}
