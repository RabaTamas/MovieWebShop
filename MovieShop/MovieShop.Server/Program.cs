using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.Models;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MovieShop.Server.Constants;
using MovieShop.Server.Services;
using MovieShop.Server.Services.Implementations;
using MovieShop.Server.Services.Interfaces;
using MovieShop.Server.Extensions;
using MovieShop.Server.Services.Implementations.TMDB;
using MovieShop.Server.Services.Interfaces.TMDB;
using Stripe;
using MovieShop.Server.Services.Implementations.Stripe;
using MovieShop.Server.Services.Interfaces.Stripe;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings.json and environment variables
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "MovieShop";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "MovieShopClient";

// Stripe configuration
var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrEmpty(stripeSecretKey))
{
    StripeConfiguration.ApiKey = stripeSecretKey;
}


// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<User, IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey ?? throw new InvalidOperationException("JWT Key is not configured")))
    };
});

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole(UserRoles.Admin));
    options.AddPolicy("RequireUserRole", policy => policy.RequireRole(UserRoles.User, UserRoles.Admin));
});

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMovieService, MovieService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReviewService, MovieShop.Server.Services.Implementations.ReviewService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddHttpClient<ITmdbService, TmdbService>();
builder.Services.AddScoped<IAdminAddressService, AdminAddressService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<IConversationService, ConversationService>();
builder.Services.AddHttpClient();

builder.Services.AddScoped<IStripeService, StripeService>();

builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.AddControllers();

// Configure CORS with environment-based origins
var allowedOrigins = builder.Configuration["CORS:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
    ?? new[] { "https://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

await app.SeedRolesAndAdminAsync();

app.Run();
