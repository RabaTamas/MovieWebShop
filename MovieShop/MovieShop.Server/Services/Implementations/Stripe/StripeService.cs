using MovieShop.Server.Services.Interfaces.Stripe;
using Stripe;

namespace MovieShop.Server.Services.Implementations.Stripe
{
    public class StripeService : IStripeService
    {
        private readonly IConfiguration _configuration;

        public StripeService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<string> CreatePaymentIntent(decimal amount, string currency = "huf")
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100),
                Currency = currency,
                PaymentMethodTypes = new List<string> { "card" }
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);

            return paymentIntent.ClientSecret;
        }

        public async Task<PaymentIntent> GetPaymentIntent(string paymentIntentId)
        {
            var service = new PaymentIntentService();
            return await service.GetAsync(paymentIntentId);
        }

        public async Task<bool> ConfirmPaymentIntent(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);

                return paymentIntent.Status == "succeeded";
            }
            catch
            {
                return false;
            }
        }
    }
}
