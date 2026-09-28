using Stripe;

namespace MovieShop.OrderService.Services;

public interface IStripeService
{
    Task<string> CreatePaymentIntent(decimal amount, string currency = "huf");
    Task<PaymentIntent> GetPaymentIntent(string paymentIntentId);
    Task<bool> ConfirmPaymentIntent(string paymentIntentId);
}

/// <summary>
/// A Stripe integráció az Order Service-hez tartozik, mert a fizetés a rendelési
/// folyamat része. Az Auction Service saját Stripe-kapcsolattal rendelkezik a
/// megnyert aukciók kifizetéséhez — ez szándékos: a két fizetési folyamat
/// függetlenül fejleszthető és skálázható.
/// </summary>
public class StripeService : IStripeService
{
    private readonly ILogger<StripeService> _logger;

    public StripeService(ILogger<StripeService> logger) => _logger = logger;

    public async Task<string> CreatePaymentIntent(decimal amount, string currency = "huf")
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(amount * 100),
            Currency = currency,
            PaymentMethodTypes = ["card"]
        };

        var paymentIntent = await new PaymentIntentService().CreateAsync(options);
        return paymentIntent.ClientSecret;
    }

    public Task<PaymentIntent> GetPaymentIntent(string paymentIntentId)
        => new PaymentIntentService().GetAsync(paymentIntentId);

    public async Task<bool> ConfirmPaymentIntent(string paymentIntentId)
    {
        try
        {
            var paymentIntent = await new PaymentIntentService().GetAsync(paymentIntentId);
            return paymentIntent.Status == "succeeded";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A fizetés ellenőrzése sikertelen: {PaymentIntentId}", paymentIntentId);
            return false;
        }
    }
}
