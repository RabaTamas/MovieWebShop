using Stripe;

namespace MovieShop.Server.Services.Interfaces.Stripe
{
    public interface IStripeService
    {
        Task<string> CreatePaymentIntent(decimal amount, string currency = "huf");
        Task<PaymentIntent> GetPaymentIntent(string paymentIntentId);

        Task<bool> ConfirmPaymentIntent(string paymentIntentId); 
    }
}
