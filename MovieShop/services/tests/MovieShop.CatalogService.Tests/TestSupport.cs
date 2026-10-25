using System.Net;
using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.Mappings;
using MovieShop.CatalogService.Models;

namespace MovieShop.CatalogService.Tests;

/// <summary>Tesztenként külön, névvel azonosított memóriabeli Catalog-adatbázis.</summary>
public sealed class CatalogDb
{
    private readonly DbContextOptions<CatalogDbContext> _options =
        new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    public CatalogDbContext Create() => new(_options);

    public Movie AddMovie(string title, int price = 1000, int? discounted = null, bool deleted = false,
        string? video = null, params string[] categories)
    {
        using var db = Create();
        var cats = categories.Select(name =>
            db.Categories.FirstOrDefault(c => c.Name == name) ?? new Category { Name = name }).ToList();
        var movie = new Movie
        {
            Title = title, Description = $"{title} leírása", Price = price, DiscountedPrice = discounted,
            ImageUrl = $"{title}.jpg", IsDeleted = deleted, VideoFileName = video, Categories = cats
        };
        db.Movies.Add(movie);
        db.SaveChanges();
        return movie;
    }

    public Category AddCategory(string name)
    {
        using var db = Create();
        var category = new Category { Name = name };
        db.Categories.Add(category);
        db.SaveChanges();
        return category;
    }

    public void AddUser(int userId, string name)
    {
        using var db = Create();
        db.UserSnapshots.Add(new UserSnapshot { UserId = userId, UserName = name, Email = $"{name}@test.hu" });
        db.SaveChanges();
    }

    public void AddReview(int movieId, int userId, string content)
    {
        using var db = Create();
        db.Reviews.Add(new Review { MovieId = movieId, UserId = userId, Content = content });
        db.SaveChanges();
    }

    public void Grant(int userId, int movieId)
    {
        using var db = Create();
        db.Entitlements.Add(new Entitlement { UserId = userId, MovieId = movieId, Source = "Order" });
        db.SaveChanges();
    }
}

public static class Mapping
{
    public static IMapper Create() =>
        new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
}

public static class ControllerUser
{
    /// <summary>Bejelentkezett felhasználó beállítása a controllerre (a JWT-ből érkező claimekkel).</summary>
    public static T As<T>(this T controller, int userId, string role = "User", string name = "tester")
        where T : ControllerBase
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Role, role)
            ], "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    public static T Anonymous<T>(this T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };
        return controller;
    }
}

/// <summary>Előre megadott válaszokat adó HTTP-kezelő a külső API-k (TMDB, Blob) helyett.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public static FakeHttpHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(json) });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(_respond(request));
    }
}

public static class Result
{
    /// <summary>Egy ObjectResult anonim törzsének egy mezője (a controllerek anonim objektumokat adnak vissza).</summary>
    public static object? Field(this IActionResult result, string name)
    {
        var value = ((ObjectResult)result).Value!;
        return value.GetType().GetProperty(name)!.GetValue(value);
    }

    public static object? Field<T>(this ActionResult<T> result, string name)
        => ((IActionResult)result.Result!).Field(name);

    public static int Status(this IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => throw new InvalidOperationException(result.GetType().Name)
    };
}
