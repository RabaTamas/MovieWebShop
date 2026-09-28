using System.Reflection;
using MovieShop.ChatService.Services;
using MovieShop.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// ── Közös service-beállítások ────────────────────────────────────────────────
// Ez az egyetlen service saját adatbázis nélkül: a beszélgetések memóriában
// élnek, minden más adat REST-en érkezik a Catalog, Order és User Service-től.
builder.Services.AddMovieShopJwtAuth(builder.Configuration);
builder.Services.AddMovieShopCors(builder.Configuration);
builder.Services.AddMovieShopMessaging(builder.Configuration, Assembly.GetExecutingAssembly(), "chat-service");
builder.Services.AddMovieShopSwagger("MovieShop Chat Service");

// ── Üzleti logika ────────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor(); // a felhasználó tokenjének továbbadásához
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IGroqClient, GroqClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(60)); // a nyelvi modell lassú tud lenni

builder.Services.AddScoped<IServiceClients, ServiceClients>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IAgentService, AgentService>();

// A munkamenetek példányszinten élnek, ezért singleton
builder.Services.AddSingleton<IConversationService, ConversationService>();
builder.Services.AddHostedService<ConversationCleanupService>();

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

app.Run();
