using System.Net;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.CatalogService.Consumers;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.Data.Interceptors;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Extensions;
using MovieShop.CatalogService.Hubs;
using MovieShop.CatalogService.Models;
using MovieShop.CatalogService.Services;
using MovieShop.Contracts.Events;

namespace MovieShop.CatalogService.Tests;

public class UserChangedConsumerTests
{
    private readonly CatalogDb _db = new();

    private Task ConsumeAsync(UserChanged msg)
    {
        var context = Substitute.For<ConsumeContext<UserChanged>>();
        context.Message.Returns(msg);
        return new UserChangedConsumer(_db.Create(), NullLogger<UserChangedConsumer>.Instance).Consume(context);
    }

    [Fact]
    public async Task Upsert_IgnoresStaleMessages()
    {
        var now = DateTime.UtcNow;
        await ConsumeAsync(new UserChanged { UserId = 7, UserName = "neo", Email = "a", OccurredAt = now });
        await ConsumeAsync(new UserChanged { UserId = 7, UserName = "régi", Email = "b", OccurredAt = now.AddMinutes(-1) });
        await ConsumeAsync(new UserChanged { UserId = 7, UserName = "neo2", Email = "c", OccurredAt = now.AddMinutes(1) });

        await using var db = _db.Create();
        var snapshot = await db.UserSnapshots.SingleAsync();
        Assert.Equal("neo2", snapshot.UserName);
        Assert.Equal("c", snapshot.Email);
    }

    [Fact]
    public async Task Delete_RemovesReviewsProgressEntitlementsAndSnapshot()
    {
        var movie = _db.AddMovie("Film");
        _db.AddUser(7, "neo");
        _db.AddReview(movie.Id, 7, "Jó");
        _db.AddReview(movie.Id, 8, "Másé");
        _db.Grant(7, movie.Id);
        await using (var db = _db.Create())
        {
            db.VideoProgresses.Add(new VideoProgress { UserId = 7, MovieId = movie.Id, ProgressSeconds = 10 });
            await db.SaveChangesAsync();
        }

        await ConsumeAsync(new UserChanged { UserId = 7, UserName = "neo", IsDeleted = true });
        await ConsumeAsync(new UserChanged { UserId = 9, UserName = "nincs", IsDeleted = true });

        await using var check = _db.Create();
        Assert.Equal(8, (await check.Reviews.SingleAsync()).UserId);
        Assert.Empty(check.VideoProgresses);
        Assert.Empty(check.Entitlements);
        Assert.Empty(check.UserSnapshots);
    }
}

public class ElasticsearchSyncInterceptorTests
{
    [Fact]
    public async Task SavedChanges_IndexesChangedMovies_AndDeletesRemoved()
    {
        var dbName = Guid.NewGuid().ToString();
        var es = Substitute.For<IElasticsearchService>();
        var done = new SemaphoreSlim(0);
        es.IndexMovieAsync(Arg.Any<Movie>()).Returns(_ => { done.Release(); return Task.CompletedTask; });
        es.DeleteMovieFromIndexAsync(Arg.Any<int>()).Returns(_ => { done.Release(); return Task.CompletedTask; });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(es);
        services.AddSingleton<ElasticsearchSyncInterceptor>();
        services.AddDbContext<CatalogDbContext>((sp, o) => o
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(sp.GetRequiredService<ElasticsearchSyncInterceptor>()));
        var provider = services.BuildServiceProvider();

        int keepId, dropId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var keep = new Movie { Title = "Marad" };
            var drop = new Movie { Title = "Törlődik" };
            db.Movies.AddRange(keep, drop);
            await db.SaveChangesAsync();
            keepId = keep.Id; dropId = drop.Id;
        }
        Assert.True(await done.WaitAsync(5000));
        Assert.True(await done.WaitAsync(5000));

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.Movies.Remove(await db.Movies.FindAsync(dropId) ?? throw new Exception());
            db.Categories.Add(new Category { Name = "csak kategória" });
            await db.SaveChangesAsync();
        }
        Assert.True(await done.WaitAsync(5000));

        await es.Received().IndexMovieAsync(Arg.Is<Movie>(m => m.Id == keepId));
        await es.Received(1).DeleteMovieFromIndexAsync(dropId);
    }

    [Fact]
    public async Task SavedChanges_WithoutMovieChanges_DoesNothing_AndIndexingErrorsAreSwallowed()
    {
        var es = Substitute.For<IElasticsearchService>();
        var called = new SemaphoreSlim(0);
        es.IndexMovieAsync(Arg.Any<Movie>()).Returns(_ => { called.Release(); throw new Exception("ES nem elérhető"); });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(es);
        services.AddSingleton<ElasticsearchSyncInterceptor>();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<CatalogDbContext>((sp, o) => o
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(sp.GetRequiredService<ElasticsearchSyncInterceptor>()));
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Categories.Add(new Category { Name = "x" });
        await db.SaveChangesAsync();
        db.Movies.Add(new Movie { Title = "Hibás indexelés" });
        await db.SaveChangesAsync();

        Assert.True(await called.WaitAsync(5000));
        await es.DidNotReceive().DeleteMovieFromIndexAsync(Arg.Any<int>());
    }
}

public class DbInitializerTests
{
    [Fact]
    public async Task Seed_CreatesCategoriesAndMovies_OnlyOnce_AndPublishesMovies()
    {
        var builder = WebApplication.CreateBuilder();
        var dbName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<CatalogDbContext>(o => o.UseInMemoryDatabase(dbName));
        var movies = Substitute.For<IMovieService>();
        builder.Services.AddSingleton(movies);
        var app = builder.Build();

        await app.SeedCatalogAsync();
        await app.SeedCatalogAsync();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        Assert.Equal(6, await db.Categories.CountAsync());
        Assert.Equal(3, await db.Movies.CountAsync());
        await movies.Received(3).PublishMovieChangedAsync(Arg.Any<int>(), false);
    }
}

public class WatchPartyHubTests
{
    private sealed class Connection
    {
        public WatchPartyHub Hub { get; }
        public IClientProxy Caller { get; } = Substitute.For<ISingleClientProxy>();
        public IClientProxy Others { get; } = Substitute.For<IClientProxy>();
        public IClientProxy Group { get; } = Substitute.For<IClientProxy>();
        public IGroupManager Groups { get; } = Substitute.For<IGroupManager>();

        public Connection(string id, string name)
        {
            var context = Substitute.For<HubCallerContext>();
            context.ConnectionId.Returns(id);
            context.User.Returns(new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, name)], "Test")));
            var clients = Substitute.For<IHubCallerClients>();
            clients.Caller.Returns((ISingleClientProxy)Caller);
            clients.OthersInGroup(Arg.Any<string>()).Returns(Others);
            clients.Group(Arg.Any<string>()).Returns(Group);
            Hub = new WatchPartyHub { Context = context, Clients = clients, Groups = Groups };
        }
    }

    private static Task Sent(IClientProxy proxy, string method)
        => proxy.Received().SendCoreAsync(method, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());

    [Fact]
    public async Task HostControlsPlayback_GuestsFollow_AndChatReachesGroup()
    {
        var host = new Connection("h-" + Guid.NewGuid(), "Anna");
        var guest = new Connection("g-" + Guid.NewGuid(), "Béla");

        var code = await host.Hub.CreateRoom(5);
        Assert.Equal(6, code.Length);
        await host.Groups.Received().AddToGroupAsync(host.Hub.Context.ConnectionId, code, Arg.Any<CancellationToken>());

        await guest.Hub.JoinRoom(code, "Béla");
        await Sent(guest.Others, "UserJoined");
        await Sent(guest.Caller, "RoomState");

        await host.Hub.Play(code, 12.5);
        await Sent(host.Others, "Play");
        Assert.NotNull(host.Hub.GetRoomState(code));
        await guest.Hub.JoinRoom(code, "Béla újra"); // lejátszás közbeni csatlakozás: becsült pozíció

        await host.Hub.Seek(code, 30);
        await Sent(host.Others, "Seek");
        await host.Hub.Pause(code, 31);
        await Sent(host.Others, "Pause");
        Assert.NotNull(host.Hub.GetRoomState(code));

        // A vendég nem vezérelheti a lejátszást
        await guest.Hub.Play(code, 99);
        await guest.Hub.Pause(code, 99);
        await guest.Hub.Seek(code, 99);
        await guest.Others.DidNotReceive().SendCoreAsync("Play", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());

        await guest.Hub.SendMessage(code, "Szia!");
        await Sent(guest.Group, "ChatMessage");
    }

    [Fact]
    public async Task UnknownRoom_IsHandled()
    {
        var c = new Connection("x-" + Guid.NewGuid(), "Cili");

        await c.Hub.JoinRoom("NOROOM", "Cili");
        await Sent(c.Caller, "Error");
        Assert.Null(c.Hub.GetRoomState("NOROOM"));
        await c.Hub.Play("NOROOM", 1);
        await c.Hub.Pause("NOROOM", 1);
        await c.Hub.Seek("NOROOM", 1);
        await c.Hub.SendMessage("NOROOM", "x");
    }

    [Fact]
    public async Task NonMember_CannotChat()
    {
        var host = new Connection("h-" + Guid.NewGuid(), "Anna");
        var outsider = new Connection("o-" + Guid.NewGuid(), "Dani");
        var code = await host.Hub.CreateRoom(5);

        await outsider.Hub.SendMessage(code, "betolakodó");

        await outsider.Group.DidNotReceive().SendCoreAsync("ChatMessage", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Disconnect_TransfersHost_AndLastLeaverClosesRoom()
    {
        var host = new Connection("h-" + Guid.NewGuid(), "Anna");
        var guest = new Connection("g-" + Guid.NewGuid(), "Béla");
        var code = await host.Hub.CreateRoom(5);
        await guest.Hub.JoinRoom(code, "Béla");

        await host.Hub.OnDisconnectedAsync(null);
        await Sent(host.Group, "HostChanged");
        await Sent(host.Group, "UserLeft");

        await guest.Hub.OnDisconnectedAsync(null);
        Assert.Null(guest.Hub.GetRoomState(code));
    }
}

public class TmdbServiceTests
{
    private static TmdbService Create(FakeHttpHandler handler, string? apiKey = "key")
        => new(new HttpClient(handler),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["TmdbApi:ApiKey"] = apiKey }).Build(),
            NullLogger<TmdbService>.Instance);

    [Fact]
    public async Task Search_ReturnsFirstResult_WithSnakeCaseFields()
    {
        var handler = FakeHttpHandler.Json("""{"results":[{"id":603,"title":"The Matrix","vote_average":8.2,"vote_count":100,"release_date":"1999-03-30"}]}""");

        var movie = await Create(handler).SearchMovieAsync("The Matrix");

        Assert.Equal(603, movie!.Id);
        Assert.Equal(8.2, movie.VoteAverage);
        Assert.Contains("query=The%20Matrix", handler.Requests.Single().RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Search_ReturnsNull_WithoutKey_OnErrorStatus_OrException()
    {
        Assert.Null(await Create(FakeHttpHandler.Json("{}"), apiKey: null).SearchMovieAsync("x"));
        Assert.Null(await Create(FakeHttpHandler.Json("{}", HttpStatusCode.InternalServerError)).SearchMovieAsync("x"));
        Assert.Null(await Create(new FakeHttpHandler(_ => throw new HttpRequestException())).SearchMovieAsync("x"));
    }

    [Fact]
    public async Task Trailer_PicksFirstYoutubeTrailer()
    {
        var handler = FakeHttpHandler.Json("""
            {"results":[{"key":"t1","name":"Teaser","site":"YouTube","type":"Teaser"},
                        {"key":"v1","name":"Vimeo","site":"Vimeo","type":"Trailer"},
                        {"key":"y1","name":"Official","site":"YouTube","type":"Trailer"}]}
            """);

        Assert.Equal("y1", (await Create(handler).GetTrailerAsync(603))!.Key);
        Assert.Null(await Create(FakeHttpHandler.Json("""{"results":null}""")).GetTrailerAsync(603));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Create(FakeHttpHandler.Json("", HttpStatusCode.NotFound)).GetTrailerAsync(603));
    }

    [Fact]
    public async Task Extras_MapsImagesCastAndDirectors()
    {
        var handler = FakeHttpHandler.Json("""
            {"id":603,"title":"The Matrix","original_title":"The Matrix","tagline":" ","overview":"o","runtime":136,
             "release_date":"","vote_average":8.2,"vote_count":10,"genres":[{"id":1,"name":"Action"}],
             "original_language":"en","budget":63000000,"revenue":463517383,"poster_path":"/p.jpg","backdrop_path":null,
             "images":{"backdrops":[{"file_path":"/b1.jpg","width":1920,"height":1080,"aspect_ratio":1.77,"vote_average":5,"iso_639_1":"en"},
                                    {"file_path":"/b2.jpg","width":1920,"height":1080,"aspect_ratio":1.77,"vote_average":1,"iso_639_1":null}],
                       "posters":[{"file_path":"/p1.jpg","width":500,"height":750,"aspect_ratio":0.66,"vote_average":3}]},
             "credits":{"cast":[{"name":"Carrie-Anne Moss","character":"Trinity","profile_path":null,"order":2},
                                {"name":"Keanu Reeves","character":"Neo","profile_path":"/k.jpg","order":0}],
                        "crew":[{"name":"Lana Wachowski","job":"Director"},{"name":"Lana Wachowski","job":"Director"},{"name":"X","job":"Writer"}]}}
            """);

        var extras = await Create(handler).GetMovieExtrasAsync(603);

        Assert.NotNull(extras);
        Assert.Null(extras.Tagline);
        Assert.Null(extras.ReleaseDate);
        Assert.Null(extras.BackdropUrl);
        Assert.Equal("https://image.tmdb.org/t/p/w500/p.jpg", extras.PosterUrl);
        Assert.Equal(["Action"], extras.Genres);
        Assert.Equal(2, extras.Backdrops.Count);
        Assert.Single(extras.Posters);
        Assert.Equal("Keanu Reeves", extras.Cast[0].Name);
        Assert.Null(extras.Cast[1].ProfileUrl);
        Assert.Equal(["Lana Wachowski"], extras.Directors);
    }

    [Fact]
    public async Task Extras_NullWithoutKey_OnError_OrEmptyBody()
    {
        Assert.Null(await Create(FakeHttpHandler.Json("{}"), apiKey: "").GetMovieExtrasAsync(1));
        Assert.Null(await Create(FakeHttpHandler.Json("{}", HttpStatusCode.NotFound)).GetMovieExtrasAsync(1));
        Assert.Null(await Create(new FakeHttpHandler(_ => throw new HttpRequestException())).GetMovieExtrasAsync(1));
        Assert.Null(await Create(FakeHttpHandler.Json("null")).GetMovieExtrasAsync(1));
        var minimal = await Create(FakeHttpHandler.Json("""{"id":1,"title":"x"}""")).GetMovieExtrasAsync(1);
        Assert.Empty(minimal!.Backdrops);
        Assert.Empty(minimal.Cast);
    }
}

public class StreamingServiceTests
{
    [Fact]
    public void SecureUrl_IsSignedForNginxSecureLink()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Streaming:SecretKey"] = "titok", ["Streaming:BaseUrl"] = "http://stream" })
            .Build();
        var service = new StreamingService(config, NullLogger<StreamingService>.Instance);

        var url = service.GenerateSecureStreamingUrl("1_master.m3u8", expiryMinutes: 10);

        Assert.StartsWith("http://stream/secure/1_master.m3u8?md5=", url);
        var md5 = url.Split("md5=")[1].Split('&')[0];
        Assert.DoesNotContain('=', md5);
        Assert.DoesNotContain('+', md5);
        Assert.DoesNotContain('/', md5);

        var defaults = new StreamingService(new ConfigurationBuilder().Build(), NullLogger<StreamingService>.Instance);
        Assert.StartsWith("http://localhost:8080/secure/x?md5=", defaults.GenerateSecureStreamingUrl("x"));
    }
}
