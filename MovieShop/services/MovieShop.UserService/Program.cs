using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieShop.ServiceDefaults;
using MovieShop.UserService.Data;
using MovieShop.UserService.Extensions;
using MovieShop.UserService.Models;
using MovieShop.UserService.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Adatréteg ────────────────────────────────────────────────────────────────
// Saját adatbázis (MovieShop.Users). Ez a service kizárólag ezt éri el —
// a filmekhez, rendelésekhez és aukciókhoz nincs és nem is lehet hozzáférése.
// Az EnableRetryOnFailure az Azure SQL serverless ébredése miatt kell: tétlenség után
// az adatbázis felfüggeszti magát, és az első kérés 40613-as hibával elszállna.
builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 6,
            maxRetryDelay: TimeSpan.FromSeconds(15),
            errorNumbersToAdd: null)));

builder.Services.AddIdentity<User, IdentityRole<int>>()
    .AddEntityFrameworkStores<UsersDbContext>()
    .AddDefaultTokenProviders();

// ── Közös service-beállítások ────────────────────────────────────────────────
builder.Services.AddMovieShopJwtAuth(builder.Configuration);
builder.Services.AddMovieShopCors(builder.Configuration);
builder.Services.AddMovieShopMessaging(builder.Configuration, Assembly.GetExecutingAssembly(), "user-service");
builder.Services.AddMovieShopSwagger("MovieShop User Service");

// ── Üzleti logika ────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IAdminAddressService, AdminAddressService>();

// Web Push értesítések (VAPID) — az „új film" értesítést a MovieChangedConsumer indítja
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddHttpClient("WebPush");

// Szinkron REST-kliens az Order Service felé (csak admin cím-képernyőkhöz)
builder.Services.AddHttpClient<IOrderServiceClient, OrderServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:OrderService"] ?? "http://localhost:5203");
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Szinkron REST-kliens az Auction Service felé (felhasználótörlés előtti licit-ellenőrzés)
builder.Services.AddHttpClient<IAuctionServiceClient, AuctionServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:AuctionService"] ?? "http://localhost:5204");
    client.Timeout = TimeSpan.FromSeconds(5);
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

// ── Indítási migrációk és seedelés ───────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        logger.LogInformation("User Service: migrációk alkalmazása...");
        await context.Database.MigrateAsync();
        logger.LogInformation("User Service: migrációk kész.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "User Service: hiba az adatbázis inicializálásakor");
        throw;
    }
}

await app.SeedRolesAndAdminAsync();

app.Run();
