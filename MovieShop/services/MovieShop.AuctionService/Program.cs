using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MovieShop.AuctionService.Data;
using MovieShop.AuctionService.Hubs;
using MovieShop.AuctionService.Services;
using MovieShop.ServiceDefaults;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrEmpty(stripeSecretKey))
    StripeConfiguration.ApiKey = stripeSecretKey;

// Az EnableRetryOnFailure az Azure SQL serverless ébredése miatt kell: tétlenség után
// az adatbázis felfüggeszti magát, és az első kérés 40613-as hibával elszállna.
builder.Services.AddDbContext<AuctionsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 6,
            maxRetryDelay: TimeSpan.FromSeconds(15),
            errorNumbersToAdd: null)));

builder.Services.AddMovieShopJwtAuth(builder.Configuration);
builder.Services.AddMovieShopCors(builder.Configuration);
builder.Services.AddMovieShopMessaging(builder.Configuration, Assembly.GetExecutingAssembly(), "auction-service");
builder.Services.AddMovieShopSwagger("MovieShop Auction Service");

builder.Services.AddScoped<IAuctionService, AuctionService>();

builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<AuctionHub>("/hubs/auction");
app.MapMovieShopHealthChecks();
app.MapMovieShopMetrics();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<AuctionsDbContext>();
        logger.LogInformation("Auction Service: migrációk alkalmazása...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Auction Service: migrációk kész.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Auction Service: hiba az adatbázis inicializálásakor");
        throw;
    }
}

app.Run();
