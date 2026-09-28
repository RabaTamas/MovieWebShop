using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MovieShop.OrderService.Data;
using MovieShop.OrderService.Services;
using MovieShop.ServiceDefaults;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

// ── Stripe ───────────────────────────────────────────────────────────────────
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrEmpty(stripeSecretKey))
    StripeConfiguration.ApiKey = stripeSecretKey;

// ── Adatréteg ────────────────────────────────────────────────────────────────
// Az EnableRetryOnFailure az Azure SQL serverless ébredése miatt kell: tétlenség után
// az adatbázis felfüggeszti magát, és az első kérés 40613-as hibával elszállna.
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 6,
            maxRetryDelay: TimeSpan.FromSeconds(15),
            errorNumbersToAdd: null)));

// ── Közös service-beállítások ────────────────────────────────────────────────
builder.Services.AddMovieShopJwtAuth(builder.Configuration);
builder.Services.AddMovieShopCors(builder.Configuration);
builder.Services.AddMovieShopMessaging(builder.Configuration, Assembly.GetExecutingAssembly(), "order-service");
builder.Services.AddMovieShopSwagger("MovieShop Order Service");

// ── Üzleti logika ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddScoped<IStripeService, StripeService>();

// Szinkron REST-kliens a User Service felé: rendeléskor a számlázási cím a monolithoz
// hasonlóan a felhasználó címei közé is bekerül (a felhasználó tokenjével).
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:UserService"] ?? "http://localhost:5201");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapMovieShopHealthChecks();
app.MapMovieShopMetrics();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        logger.LogInformation("Order Service: migrációk alkalmazása...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Order Service: migrációk kész.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Order Service: hiba az adatbázis inicializálásakor");
        throw;
    }
}

app.Run();
