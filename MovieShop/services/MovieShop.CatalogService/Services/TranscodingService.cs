using System.Diagnostics;

namespace MovieShop.CatalogService.Services;

public interface ITranscodingService
{
    Task TranscodeVideoAsync(int movieId, string originalFileName);
}

/// <summary>
/// FFmpeg-alapú HLS transzkódolás: az eredeti videóból három felbontású
/// szegmenssorozat készül, majd egy mesterlejátszási lista fogja össze őket.
///
/// A művelet percekig tarthat, ezért háttérfeladatként fut (AdminVideoController
/// indítja el, nem várja meg). A monolithoz képest a végén a MovieService-en
/// keresztül frissül a VideoFileName, ami egyúttal MovieChanged eseményt is publikál.
/// </summary>
public class TranscodingService : ITranscodingService
{
    private static readonly (string Name, int Height, string Bitrate)[] Resolutions =
    [
        ("480p", 480, "1000k"),
        ("720p", 720, "2500k"),
        ("1080p", 1080, "5000k")
    ];

    private readonly IBlobStorageService _blobStorage;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TranscodingService> _logger;

    public TranscodingService(
        IBlobStorageService blobStorage,
        IServiceProvider serviceProvider,
        ILogger<TranscodingService> logger)
    {
        _blobStorage = blobStorage;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task TranscodeVideoAsync(int movieId, string originalFileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"transcode_{movieId}_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            _logger.LogInformation(
                "Transzkódolás indul — film: {MovieId}, fájl: {FileName}", movieId, originalFileName);

            var originalPath = await _blobStorage.DownloadToTempAsync(originalFileName);
            var baseFileName = Path.GetFileNameWithoutExtension(originalFileName);

            foreach (var resolution in Resolutions)
            {
                var playlistName = $"{baseFileName}_{resolution.Name}.m3u8";
                var segmentPattern = $"{baseFileName}_{resolution.Name}_%03d.ts";
                var outputPlaylistPath = Path.Combine(tempDir, playlistName);

                _logger.LogInformation("Transzkódolás {Resolution} felbontásra...", resolution.Name);

                var ffmpegArgs =
                    $"-i \"{originalPath}\" " +
                    $"-vf scale=-2:{resolution.Height} " +
                    $"-c:v libx264 -b:v {resolution.Bitrate} " +
                    $"-c:a aac -b:a 128k " +
                    $"-f hls -hls_time 6 -hls_playlist_type vod " +
                    $"-hls_segment_filename \"{Path.Combine(tempDir, segmentPattern)}\" " +
                    $"-y \"{outputPlaylistPath}\"";

                await RunFFmpegAsync(ffmpegArgs);

                await using (var playlistStream = File.OpenRead(outputPlaylistPath))
                    await _blobStorage.UploadVideoAsync(playlistStream, playlistName);

                var segmentFiles = Directory.GetFiles(tempDir, $"{baseFileName}_{resolution.Name}_*.ts");
                _logger.LogInformation(
                    "{Count} szegmens feltöltése ({Resolution})...", segmentFiles.Length, resolution.Name);

                foreach (var segmentFile in segmentFiles)
                {
                    await using (var segmentStream = File.OpenRead(segmentFile))
                        await _blobStorage.UploadVideoAsync(segmentStream, Path.GetFileName(segmentFile));

                    File.Delete(segmentFile);
                }

                File.Delete(outputPlaylistPath);
            }

            // Mesterlejátszási lista összeállítása
            var masterPlaylistName = $"{baseFileName}_master.m3u8";
            var masterPlaylistPath = Path.Combine(tempDir, masterPlaylistName);

            var masterContent = "#EXTM3U\n#EXT-X-VERSION:3\n";
            foreach (var resolution in Resolutions)
            {
                var bandwidth = resolution.Bitrate.Replace("k", "000");
                var width = (int)Math.Round(resolution.Height * 16.0 / 9.0);

                masterContent += $"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={width}x{resolution.Height}\n";
                masterContent += $"{baseFileName}_{resolution.Name}.m3u8\n";
            }

            await File.WriteAllTextAsync(masterPlaylistPath, masterContent);

            await using (var masterStream = File.OpenRead(masterPlaylistPath))
                await _blobStorage.UploadVideoAsync(masterStream, masterPlaylistName);

            File.Delete(masterPlaylistPath);

            // A VideoFileName frissítése egyben MovieChanged eseményt is publikál
            using var scope = _serviceProvider.CreateScope();
            var movieService = scope.ServiceProvider.GetRequiredService<IMovieService>();
            await movieService.UpdateVideoFileNameAsync(movieId, masterPlaylistName);

            _logger.LogInformation("Transzkódolás kész — film: {MovieId}", movieId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A transzkódolás sikertelen — film: {MovieId}", movieId);
            throw;
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private async Task RunFFmpegAsync(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data)) _logger.LogDebug("FFmpeg: {Output}", args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrEmpty(args.Data)) _logger.LogDebug("FFmpeg: {Output}", args.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg exited with code {process.ExitCode}");
    }
}
