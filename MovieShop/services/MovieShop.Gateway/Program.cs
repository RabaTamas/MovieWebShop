using MovieShop.ServiceDefaults;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ── Ocelot útvonaltábla ──────────────────────────────────────────────────────
// Lokálisan a Docker-hálózat nevei és a 8080-as port érvényes. A felhőben a
// Container Apps a belső hívásokat a 80-as porton szolgálja ki, ezért ott az
// ocelot.cloud.json töltődik be (Ocelot__ConfigFile környezeti változóval).
var ocelotConfigFile = builder.Configuration["Ocelot:ConfigFile"] ?? "ocelot.json";
builder.Configuration.AddJsonFile(ocelotConfigFile, optional: false, reloadOnChange: true);

// Az AddQualityOfService() nélkül az Ocelot elutasítja az indulást, ha bármelyik
// útvonal QoSOptions-t tartalmaz. Ez adja az időtúllépés-kezelést (nagy
// videófeltöltés, lassú nyelvi modell) és a megszakítót (circuit breaker) is:
// ha egy service sorozatosan hibázik, a gateway átmenetileg abbahagyja a hívását
// ahelyett, hogy minden kérésnél kivárná az időtúllépést.
builder.Services
    .AddOcelot(builder.Configuration)
    .AddQualityOfService();

// ── Hitelesítés ──────────────────────────────────────────────────────────────
// A gateway ismeri a JWT-t, de NEM kényszeríti ki: a tokent változtatás nélkül
// továbbadja, és az érvényesítést minden service önállóan végzi.
//
// Ez szándékos döntés. A jogosultsági szabályok (ki melyik filmet nézheti, kié
// az értékelés, admin-e a felhasználó) az üzleti logikához tartoznak, nem az
// útválasztáshoz. Ha a gateway is döntene róluk, a szabályok két helyen élnének,
// és el is csúszhatnának egymástól. Így a gateway felelőssége egyetlen dolog:
// a kérés a megfelelő service-hez jusson.
builder.Services.AddMovieShopJwtAuth(builder.Configuration);

// ── CORS ─────────────────────────────────────────────────────────────────────
// Ez az egyetlen pont, ahol a böngésző az API-val találkozik, ezért a CORS
// konfiguráció is ide tartozik.
builder.Services.AddMovieShopCors(builder.Configuration);

// Nagy videófájlok átengedése (500 MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors();

// A SignalR WebSocket-kapcsolatok átengedéséhez
app.UseWebSockets();

// Prometheus-metrikák. Ugyanaz a korlát érvényes, mint a /health-nél: a MapMetrics()
// végpont-alapú lenne, azt az Ocelot elnyelné — ezért a prometheus-net middleware-es
// változatát használjuk, az UseOcelot() ELŐTT.
//   UseMetricServer  -> a /metrics végpont kiszolgálása
//   UseHttpMetrics   -> a gatewayen áthaladó kérések válaszideje és darabszáma
app.UseMetricServer("/metrics");
app.UseHttpMetrics();

// FIGYELEM: a gateway saját végpontjait NEM lehet MapGet/MapHealthChecks hívással
// felvenni. Azok a végpont-útválasztóba kerülnek, ami a pipeline VÉGÉN fut le —
// az Ocelot viszont már előbb elkapja a kérést, és ismeretlen útvonalként 404-gyel
// elutasítja. Ezért a gateway saját útvonalai közvetlen middleware-ágakként
// szerepelnek, az UseOcelot() ELŐTT.
app.Map("/health", branch => branch.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "text/plain";
    await context.Response.WriteAsync("Healthy");
}));

// Rövid tájékoztató a gyökéren, hogy egy böngészős találat se legyen üres 404.
// A gyökérre `app.Map("/")` nem használható (a Map nem fogad '/'-re végződő
// útvonalat), ezért feltételes middleware kezeli.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/")
    {
        await context.Response.WriteAsJsonAsync(new
        {
            service = "MovieShop API Gateway",
            version = "1.0",
            services = new[] { "user", "catalog", "order", "auction", "chat" },
            health = "/health"
        });
        return;
    }

    await next();
});

// Az Ocelot middleware-nek a pipeline VÉGÉN kell állnia: ami idáig eljut,
// azt továbbítja a megfelelő service-nek.
await app.UseOcelot();

app.Run();
