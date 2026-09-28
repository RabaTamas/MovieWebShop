using System.Reflection;
using Elasticsearch.Net;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.Data.Interceptors;
using MovieShop.CatalogService.Extensions;
using MovieShop.CatalogService.Hubs;
using MovieShop.CatalogService.Services;
using MovieShop.ServiceDefaults;
using Nest;

var builder = WebApplication.CreateBuilder(args);

// Nagy videófájlok fogadása (500 MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
});

builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 524_288_000);

// ── Keresés (Elasticsearch vagy SQL-alapú tartalék) ──────────────────────────
// Lokálisan Elasticsearch fut (fuzzy keresés, relevancia). A felhőben nincs hozzá
// olcsó menedzselt szolgáltatás, ezért ott a Search:Provider=sql beállítással a
// keresés közvetlenül az adatbázisból megy — a végpontok és a válaszok azonosak.
var searchProvider = builder.Configuration["Search:Provider"];
var esUrl = builder.Configuration["Elasticsearch:Url"];
var useElasticsearch =
    !string.Equals(searchProvider, "sql", StringComparison.OrdinalIgnoreCase) &&
    !string.IsNullOrWhiteSpace(esUrl);

if (useElasticsearch)
{
    var esSettings = new ConnectionSettings(new Uri(esUrl!))
        .DefaultIndex("movies")
        .EnableApiVersioningHeader(false);

    builder.Services.AddSingleton<IElasticClient>(new ElasticClient(esSettings));
    builder.Services.AddScoped<IElasticsearchService, ElasticsearchService>();
}
else
{
    builder.Services.AddScoped<IElasticsearchService, SqlSearchService>();
}

builder.Services.AddSingleton<ElasticsearchSyncInterceptor>();

// ── Adatréteg ────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<CatalogDbContext>((sp, options) =>
{
    // Újrapróbálkozás átmeneti hibákra. Felhőben az Azure SQL serverless szintje
    // tétlenség után felfüggeszti magát, és az ébredés alatt (~30-60 mp) a hívások
    // 40613-as hibával elszállnának — ezzel a beállítással az EF csendben újrapróbálja.
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
        maxRetryCount: 6,
        maxRetryDelay: TimeSpan.FromSeconds(15),
        errorNumbersToAdd: null));
    options.AddInterceptors(sp.GetRequiredService<ElasticsearchSyncInterceptor>());
});

// ── Hangfire (transzkódolás háttérfeladatként, a monolittal azonos beállítással) ──
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer();

// ── Közös service-beállítások ────────────────────────────────────────────────
builder.Services.AddMovieShopJwtAuth(builder.Configuration);
builder.Services.AddMovieShopCors(builder.Configuration);
builder.Services.AddMovieShopMessaging(builder.Configuration, Assembly.GetExecutingAssembly(), "catalog-service");
builder.Services.AddMovieShopSwagger("MovieShop Catalog Service");

// ── Üzleti logika ────────────────────────────────────────────────────────────
builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddScoped<IMovieService, MovieService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IEntitlementService, EntitlementService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<ITranscodingService, TranscodingService>();
builder.Services.AddScoped<IStreamingService, StreamingService>();
builder.Services.AddHttpClient<ITmdbService, TmdbService>();
builder.Services.AddHttpClient();

builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Indítási migrációk ───────────────────────────────────────────────────────
// A migrációknak a Hangfire dashboard ELŐTT kell lefutniuk: a dashboard azonnal
// megnyitja a Hangfire SQL-tárolóját, ami friss telepítésnél még nem létező
// adatbázisra csatlakozna.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        logger.LogInformation("Catalog Service: applying migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Catalog Service: migrations applied.");

        var es = scope.ServiceProvider.GetRequiredService<IElasticsearchService>();
        await es.EnsureIndexExistsAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Catalog Service: error initializing the database");
        throw;
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard (a monolithoz hasonlóan, alapértelmezett helyi hozzáférés-szabállyal)
app.UseHangfireDashboard("/hangfire");

app.MapControllers();
app.MapHub<WatchPartyHub>("/hubs/watchparty");
app.MapMovieShopHealthChecks();
app.MapMovieShopMetrics();

await app.SeedCatalogAsync();

app.Run();
