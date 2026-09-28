using System.Reflection;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;

namespace MovieShop.ServiceDefaults;

/// <summary>
/// Minden mikroszerviz ugyanezt a néhány hívást használja a bootstrapeléshez.
/// A cél, hogy a JWT-validáció, a CORS, a Swagger és az üzenetsor-kapcsolat
/// pontosan ugyanúgy legyen beállítva mindenhol — egy helyen karbantartva.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>
    /// JWT bearer hitelesítés. Minden service ugyanazzal a szimmetrikus kulccsal
    /// validál, de csak a User Service állít ki tokent — a többi kizárólag ellenőriz.
    /// Adatbázis-lekérdezés nem történik: a token önhordó, ezért a hitelesítés
    /// service-ek között is állapotmentes marad.
    /// </summary>
    public static IServiceCollection AddMovieShopJwtAuth(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key nincs konfigurálva");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "MovieShop";
        var jwtAudience = configuration["Jwt:Audience"] ?? "MovieShopClient";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };

            // A SignalR WebSocket nem tud HTTP-fejlécet küldeni, ezért a hubok
            // a query stringből olvassák ki a tokent — ugyanúgy, mint a monolitban.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) &&
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
            options.AddPolicy("RequireUserRole", policy => policy.RequireRole("User", "Admin"));
        });

        return services;
    }

    /// <summary>
    /// CORS. A gateway mögött a böngésző mindig a gatewayjel beszél, de a service-ek
    /// fejlesztéskor közvetlenül is hívhatók, ezért mindenhol be van állítva.
    /// </summary>
    public static IServiceCollection AddMovieShopCors(
        this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration["CORS:AllowedOrigins"]
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? ["http://localhost:3000", "https://localhost:5173"];

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        return services;
    }

    /// <summary>
    /// MassTransit + RabbitMQ. A megadott assemblyben található összes IConsumer
    /// automatikusan regisztrálódik, így egy új eseménykezelő felvételéhez nem kell
    /// a bootstrap kódhoz nyúlni.
    ///
    /// A kicserélhetőség szándékos: az UsingRabbitMq egyetlen sorának UsingAzureServiceBus-ra
    /// cserélésével az egész rendszer átáll — a consumer-osztályok változatlanok maradnak.
    /// </summary>
    public static IServiceCollection AddMovieShopMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly consumerAssembly,
        string serviceName)
    {
        // Felhőben Azure Service Bus, lokálisan RabbitMQ — a választás egyetlen
        // konfigurációs értéken múlik (ServiceBus__ConnectionString). A consumer
        // osztályok, az események és a sornevek mindkét esetben ugyanazok.
        var serviceBusConnection = configuration["ServiceBus:ConnectionString"];

        var host = configuration["RabbitMq:Host"] ?? "localhost";
        var username = configuration["RabbitMq:Username"] ?? "guest";
        var password = configuration["RabbitMq:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            x.AddConsumers(consumerAssembly);

            // A SERVICE-ENKÉNTI ELŐTAG ELHAGYHATATLAN.
            //
            // Prefix nélkül minden service ugyanazt a sornevet kapná (pl. "movie-changed"),
            // mert a név a MESSAGE típusából képződik. Ilyenkor a RabbitMQ egyetlen sorra
            // köti be mindkét fogyasztót, és az üzeneteket KÖRBEOSZTJA közöttük (competing
            // consumers) ahelyett, hogy mindenkinek elküldené — vagyis az Order és az
            // Auction Service felváltva kapná a filmváltozásokat, a snapshotjaik pedig
            // csendben, véletlenszerűen szétcsúsznának.
            //
            // Az előtaggal minden service saját sort kap ("order-service-movie-changed",
            // "auction-service-movie-changed"), amiket a broker ugyanarra az exchange-re
            // köt be — így mindkettő megkap MINDEN eseményt (publish/subscribe).
            x.SetEndpointNameFormatter(
                new KebabCaseEndpointNameFormatter(prefix: serviceName, includeNamespace: false));

            if (!string.IsNullOrWhiteSpace(serviceBusConnection))
            {
                x.UsingAzureServiceBus((context, cfg) =>
                {
                    cfg.Host(serviceBusConnection);

                    cfg.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15)));

                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(host, "/", h =>
                    {
                        h.Username(username);
                        h.Password(password);
                    });

                    // Átmeneti hibáknál (broker újraindulás, hálózati szakadás) újrapróbálkozás,
                    // utána a dead-letter queue-ba kerül az üzenet — nem vész el némán.
                    cfg.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15)));

                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }

    /// <summary>Swagger UI JWT bearer beviteli mezővel, hogy a védett végpontok is kipróbálhatók legyenek.</summary>
    public static IServiceCollection AddMovieShopSwagger(this IServiceCollection services, string title)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1" });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT token. Formátum: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Health check végpont. A docker-compose ezt használja a `depends_on:
    /// condition: service_healthy` feltételhez, így a gateway csak akkor indul,
    /// ha a mögötte lévő service-ek már válaszképesek.
    /// </summary>
    public static WebApplication MapMovieShopHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        return app;
    }

    /// <summary>
    /// Prometheus-metrikák: a service-enkénti válaszidő, kérésszám és hibaarány,
    /// valamint a .NET futásidejű mutatói (GC, szálkészlet, memória) a `/metrics`
    /// végponton jelennek meg, Prometheus által olvasható formában.
    ///
    /// Miért a prometheus-net és nem az OpenTelemetry: az OpenTelemetry Prometheus
    /// exportere máig csak előzetes (beta) kiadásban létezik, a projekt viszont
    /// mindenhol stabil csomagverziókat rögzít.
    ///
    /// A UseHttpMetrics() a kérések feldolgozását méri, ezért a végpontok
    /// kiszolgálása ELŐTT kell a folyamatba kerülnie — a minimal hosting a
    /// végpont-middleware-t a lánc végére teszi, így ez a hívási hely megfelelő.
    /// </summary>
    public static WebApplication MapMovieShopMetrics(this WebApplication app)
    {
        app.UseHttpMetrics();
        app.MapMetrics("/metrics");
        return app;
    }
}
