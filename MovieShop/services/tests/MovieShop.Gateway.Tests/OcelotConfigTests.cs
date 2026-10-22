using System.Text.Json;

namespace MovieShop.Gateway.Tests;

/// <summary>
/// Az útvonaltábla a rendszer kifelé látszó szerződése. Ezek a tesztek a két
/// telepítés (Docker Compose és Azure Container Apps) konfigurációját vetik össze.
/// </summary>
public class OcelotConfigTests
{
    private static readonly string[] Services =
        ["userservice", "catalogservice", "orderservice", "auctionservice", "chatservice"];

    public static TheoryData<string> ConfigFiles => new() { "ocelot.json", "ocelot.cloud.json" };

    private record Route(string Upstream, string Downstream, string Host, string Scheme, JsonElement Raw);

    private static List<Route> LoadRoutes(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file)),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

        return doc.RootElement.GetProperty("Routes").EnumerateArray()
            .Select(r => new Route(
                r.GetProperty("UpstreamPathTemplate").GetString()!,
                r.GetProperty("DownstreamPathTemplate").GetString()!,
                r.GetProperty("DownstreamHostAndPorts")[0].GetProperty("Host").GetString()!,
                r.GetProperty("DownstreamScheme").GetString()!,
                r.Clone()))
            .ToList();
    }

    private static string ServiceOf(Route route) => route.Host.Split('.')[0];

    [Theory]
    [MemberData(nameof(ConfigFiles))]
    public void InternalEndpoints_AreNotExposed(string file)
    {
        var routes = LoadRoutes(file);

        Assert.DoesNotContain(routes, r => r.Upstream.Contains("/internal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(routes, r => r.Downstream.Contains("/internal", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(ConfigFiles))]
    public void EveryRoute_TargetsAKnownService(string file)
    {
        Assert.All(LoadRoutes(file), r => Assert.Contains(ServiceOf(r), Services));
    }

    [Theory]
    [MemberData(nameof(ConfigFiles))]
    public void EveryService_IsReachableThroughTheGateway(string file)
    {
        var targeted = LoadRoutes(file).Select(ServiceOf).ToHashSet();

        Assert.All(Services, s => Assert.Contains(s, targeted));
    }

    [Theory]
    [MemberData(nameof(ConfigFiles))]
    public void UpstreamTemplates_AreUnique(string file)
    {
        var duplicates = LoadRoutes(file)
            .GroupBy(r => r.Upstream, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        Assert.Empty(duplicates);
    }

    [Theory]
    [InlineData("ocelot.json", "ws")]
    [InlineData("ocelot.cloud.json", "wss")]
    public void SignalRHubs_HaveNegotiateAndWebSocketRoutes(string file, string wsScheme)
    {
        var routes = LoadRoutes(file);

        foreach (var hub in new[] { "/hubs/auction", "/hubs/watchparty" })
        {
            Assert.Contains(routes, r => r.Upstream == $"{hub}/negotiate");
            Assert.Contains(routes, r => r.Upstream == hub && r.Scheme == wsScheme);
        }
    }

    [Theory]
    [MemberData(nameof(ConfigFiles))]
    public void SlowRoutes_HaveCircuitBreaker(string file)
    {
        var routes = LoadRoutes(file);

        foreach (var prefix in new[] { "/api/admin/Video", "/api/Chat" })
        {
            var route = routes.Single(r => r.Upstream.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            var qos = route.Raw.GetProperty("QoSOptions");
            Assert.True(qos.GetProperty("TimeoutValue").GetInt32() > 0);
            Assert.True(qos.GetProperty("ExceptionsAllowedBeforeBreaking").GetInt32() > 0);
        }
    }

    [Fact]
    public void LocalAndCloudDeployments_ExposeTheSameApi()
    {
        // A két független telepítésnek azonos üzleti funkciókat kell nyújtania
        static HashSet<string> Signature(string file)
            => LoadRoutes(file).Select(r => $"{r.Upstream} -> {ServiceOf(r)}{r.Downstream}").ToHashSet();

        var local = Signature("ocelot.json");
        var cloud = Signature("ocelot.cloud.json");

        Assert.Empty(local.Except(cloud));
        Assert.Empty(cloud.Except(local));
    }
}
