using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieShop.CatalogService.Services;

namespace MovieShop.CatalogService.Tests;

/// <summary>
/// Az Azure Blob Storage-ot az Azurite emulátor helyettesíti (CI-ben service-konténer,
/// helyben: npx azurite-blob). Ha az emulátor nem fut, az integrációs esetek kimaradnak.
/// </summary>
public class BlobStorageServiceTests
{
    private const string Azurite =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    private static bool AzuriteRunning()
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", 10000).Wait(2000);
        }
        catch { return false; }
    }

    private static BlobStorageService Create(string? connectionString, string container = "test-videos")
        => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureBlob:ConnectionString"] = connectionString,
            ["AzureBlob:ContainerName"] = container,
            ["CORS:AllowedOrigins"] = "http://localhost:3001, https://frontend.test"
        }).Build(), NullLogger<BlobStorageService>.Instance);

    [Fact]
    public async Task WithoutConfiguration_StreamingIsUnavailable_ButServiceStarts()
    {
        var service = Create(null);

        Assert.False(await service.VideoExistsAsync("x.mp4"));
        Assert.Equal(0, await service.GetVideoSizeAsync("x.mp4"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadVideoAsync(Stream.Null, "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateSasUrlAsync("x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListFilesAsync());
    }

    [Fact]
    public async Task InvalidConnectionString_IsLoggedNotThrown()
    {
        var service = Create("nem-connection-string");

        Assert.False(await service.VideoExistsAsync("x"));
    }

    [SkippableFact]
    public async Task FullLifecycle_AgainstAzurite()
    {
        Skip.IfNot(AzuriteRunning(), "Az Azurite emulátor nem fut a 127.0.0.1:10000 címen");

        var service = Create(Azurite, $"c{Guid.NewGuid():N}"[..20]);
        var name = $"{Guid.NewGuid():N}_480p_000.ts";

        var uri = await service.UploadVideoAsync(new MemoryStream(new byte[2048]), name);
        Assert.Contains(name, uri);

        Assert.True(await service.VideoExistsAsync(name));
        Assert.Equal(2048, await service.GetVideoSizeAsync(name));
        Assert.Equal(0, await service.GetVideoSizeAsync("hianyzo.ts"));
        Assert.Contains(name, await service.ListFilesAsync(name[..8]));

        var sas = await service.GenerateSasUrlAsync(name, expiryHours: 2);
        Assert.Contains("sig=", sas);
        Assert.Contains("sp=r", sas);
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.GenerateSasUrlAsync("hianyzo.ts"));

        var temp = await service.DownloadToTempAsync(name);
        Assert.Equal(2048, new FileInfo(temp).Length);
        File.Delete(temp);

        await service.DeleteVideoAsync(name);
        Assert.False(await service.VideoExistsAsync(name));
    }
}

/// <summary>
/// A transzkódolás valódi FFmpeg-gel fut (a CI telepíti). A sikeres út csak akkor fut,
/// ha az ffmpeg elérhető; a hibaág mindenhol fut.
/// </summary>
public class TranscodingServiceTests
{
    private static bool FfmpegAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
                { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            p!.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private readonly IBlobStorageService _blob = Substitute.For<IBlobStorageService>();
    private readonly IMovieService _movies = Substitute.For<IMovieService>();
    private readonly List<string> _uploaded = [];

    private TranscodingService Create()
    {
        _blob.UploadVideoAsync(Arg.Any<Stream>(), Arg.Any<string>())
            .Returns(ci => { _uploaded.Add(ci.ArgAt<string>(1)); return "uri"; });
        var services = new ServiceCollection().AddSingleton(_movies).BuildServiceProvider();
        return new TranscodingService(_blob, services, NullLogger<TranscodingService>.Instance);
    }

    [SkippableFact]
    public async Task Transcode_ProducesThreeQualities_MasterPlaylist_AndUpdatesMovie()
    {
        Skip.IfNot(FfmpegAvailable(), "Az ffmpeg nincs telepítve");

        var source = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        using (var gen = Process.Start(new ProcessStartInfo("ffmpeg",
                   $"-f lavfi -i testsrc=duration=2:size=320x240:rate=10 -f lavfi -i sine=duration=2 -shortest -y \"{source}\"")
                   { RedirectStandardError = true, UseShellExecute = false })!)
        {
            await gen.StandardError.ReadToEndAsync();
            await gen.WaitForExitAsync();
        }
        _blob.DownloadToTempAsync("42.mp4").Returns(source);

        await Create().TranscodeVideoAsync(42, "42.mp4");

        Assert.Contains("42_480p.m3u8", _uploaded);
        Assert.Contains("42_720p.m3u8", _uploaded);
        Assert.Contains("42_1080p.m3u8", _uploaded);
        Assert.Contains("42_master.m3u8", _uploaded);
        Assert.Contains(_uploaded, f => f.StartsWith("42_720p_") && f.EndsWith(".ts"));
        await _movies.Received(1).UpdateVideoFileNameAsync(42, "42_master.m3u8");
        File.Delete(source);
    }

    [Fact]
    public async Task Transcode_Failure_IsRethrown_AndMovieIsNotUpdated()
    {
        _blob.DownloadToTempAsync(Arg.Any<string>()).Returns(Path.Combine(Path.GetTempPath(), "nem-letezo.mp4"));

        await Assert.ThrowsAnyAsync<Exception>(() => Create().TranscodeVideoAsync(1, "1.mp4"));

        await _movies.DidNotReceive().UpdateVideoFileNameAsync(Arg.Any<int>(), Arg.Any<string?>());
        Assert.Empty(_uploaded);
    }
}
