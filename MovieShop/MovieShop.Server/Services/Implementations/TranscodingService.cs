using MovieShop.Server.Data;
using MovieShop.Server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace MovieShop.Server.Services.Implementations
{
    public class TranscodingService : ITranscodingService
    {
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
                _logger.LogInformation($"Starting transcoding for Movie ID: {movieId}, File: {originalFileName}");

                // Download original video from Azure Blob
                var originalPath = await _blobStorage.DownloadToTempAsync(originalFileName);
                _logger.LogInformation($"Downloaded original video to: {originalPath}");

                // Define output resolutions for HLS
                var resolutions = new (string Name, int Height, string Bitrate)[]
                {
                    ("480p", 480, "1000k"),
                    ("720p", 720, "2500k"),
                    ("1080p", 1080, "5000k")
                };

                var baseFileName = Path.GetFileNameWithoutExtension(originalFileName);
                var playlistFiles = new List<string>();

                // Transcode to each resolution with HLS
                foreach (var resolution in resolutions)
                {
                    var playlistName = $"{baseFileName}_{resolution.Name}.m3u8";
                    var segmentPattern = $"{baseFileName}_{resolution.Name}_%03d.ts";
                    var outputPlaylistPath = Path.Combine(tempDir, playlistName);

                    _logger.LogInformation($"Transcoding to HLS {resolution.Name}...");

                    // FFmpeg command: transcode to HLS with segments
                    var ffmpegArgs = $"-i \"{originalPath}\" " +
                                     $"-vf scale=-2:{resolution.Height} " +
                                     $"-c:v libx264 " +
                                     $"-b:v {resolution.Bitrate} " +
                                     $"-c:a aac " +
                                     $"-b:a 128k " +
                                     $"-f hls " +
                                     $"-hls_time 6 " +
                                     $"-hls_playlist_type vod " +
                                     $"-hls_segment_filename \"{Path.Combine(tempDir, segmentPattern)}\" " +
                                     $"-y \"{outputPlaylistPath}\"";

                    await RunFFmpegAsync(ffmpegArgs);

                    // Upload playlist file
                    _logger.LogInformation($"Uploading {playlistName}...");
                    using (var playlistStream = File.OpenRead(outputPlaylistPath))
                    {
                        await _blobStorage.UploadVideoAsync(playlistStream, playlistName);
                    }
                    playlistFiles.Add(playlistName);

                    // Upload all segment files
                    var segmentFiles = Directory.GetFiles(tempDir, $"{baseFileName}_{resolution.Name}_*.ts");
                    _logger.LogInformation($"Uploading {segmentFiles.Length} segments for {resolution.Name}...");
                    
                    foreach (var segmentFile in segmentFiles)
                    {
                        var segmentFileName = Path.GetFileName(segmentFile);
                        using var segmentStream = File.OpenRead(segmentFile);
                        await _blobStorage.UploadVideoAsync(segmentStream, segmentFileName);
                        File.Delete(segmentFile);
                    }

                    File.Delete(outputPlaylistPath);
                }

                // Create master playlist
                var masterPlaylistName = $"{baseFileName}_master.m3u8";
                var masterPlaylistPath = Path.Combine(tempDir, masterPlaylistName);
                
                _logger.LogInformation("Creating HLS master playlist...");
                var masterContent = "#EXTM3U\n#EXT-X-VERSION:3\n";
                
                foreach (var resolution in resolutions)
                {
                    var bandwidth = resolution.Bitrate.Replace("k", "000");
                    var playlistName = $"{baseFileName}_{resolution.Name}.m3u8";
                    masterContent += $"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={GetWidth(resolution.Height)}x{resolution.Height}\n";
                    masterContent += $"{playlistName}\n";
                }
                
                File.WriteAllText(masterPlaylistPath, masterContent);
                
                // Upload master playlist
                _logger.LogInformation($"Uploading master playlist {masterPlaylistName}...");
                using (var masterStream = File.OpenRead(masterPlaylistPath))
                {
                    await _blobStorage.UploadVideoAsync(masterStream, masterPlaylistName);
                }
                File.Delete(masterPlaylistPath);

                // Mark transcoding as complete in database
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var movie = await dbContext.Movies.FindAsync(movieId);
                if (movie != null)
                {
                    // Store master playlist filename
                    movie.VideoFileName = masterPlaylistName;
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation($"Movie {movieId} updated with HLS master playlist: {movie.VideoFileName}");
                }

                _logger.LogInformation($"Transcoding completed for Movie ID: {movieId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Transcoding failed for Movie ID: {movieId}");
                throw;
            }
            finally
            {
                // Cleanup temp directory
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private async Task RunFFmpegAsync(string arguments)
        {
            var process = new Process
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

            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    _logger.LogDebug($"FFmpeg: {args.Data}");
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    _logger.LogDebug($"FFmpeg: {args.Data}");
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"FFmpeg exited with code {process.ExitCode}");
            }
        }

        private int GetWidth(int height)
        {
            // Calculate width maintaining 16:9 aspect ratio
            return (int)Math.Round(height * 16.0 / 9.0);
        }
    }
}
