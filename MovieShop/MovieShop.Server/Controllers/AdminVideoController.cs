using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.Services.Interfaces;
using Hangfire;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize(Policy = "RequireAdminRole")]
    public class VideoController : ControllerBase
    {
        private readonly IMovieService _movieService;
        private readonly IBlobStorageService _blobStorage;
        private readonly ILogger<VideoController> _logger;

        public VideoController(
            IMovieService movieService,
            IBlobStorageService blobStorage,
            ILogger<VideoController> logger)
        {
            _movieService = movieService;
            _blobStorage = blobStorage;
            _logger = logger;
        }

        [HttpPost("upload/{movieId}")]
        [RequestSizeLimit(524288000)] // 500 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
        public async Task<IActionResult> UploadVideo(int movieId, IFormFile videoFile)
        {
            if (videoFile == null || videoFile.Length == 0)
            {
                return BadRequest(new { message = "No video file provided" });
            }

            // Validate file extension
            var extension = Path.GetExtension(videoFile.FileName).ToLowerInvariant();
            if (extension != ".mp4")
            {
                return BadRequest(new { message = "Only MP4 files are supported" });
            }

            // Check if movie exists
            var movie = await _movieService.GetMovieByIdAsync(movieId);
            if (movie == null)
            {
                return NotFound(new { message = "Movie not found" });
            }

            try
            {
                // Generate filename: {movieId}.mp4 (original file)
                var originalFileName = $"{movieId}.mp4";

                // Upload original video to Azure Blob Storage
                using (var stream = videoFile.OpenReadStream())
                {
                    await _blobStorage.UploadVideoAsync(stream, originalFileName);
                }

                _logger.LogInformation($"Original video uploaded to Azure Blob for movie {movieId}: {originalFileName}");

                // Trigger Hangfire background job for transcoding
                var jobId = BackgroundJob.Enqueue<ITranscodingService>(
                    x => x.TranscodeVideoAsync(movieId, originalFileName)
                );

                _logger.LogInformation($"Transcoding job queued for movie {movieId}, JobId: {jobId}");

                return Ok(new
                {
                    message = "Video uploaded successfully. Transcoding started in background.",
                    originalFileName = originalFileName,
                    fileSize = videoFile.Length,
                    movieId = movieId,
                    jobId = jobId,
                    status = "transcoding"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading video for movie {movieId}");
                return StatusCode(500, new { message = $"Error uploading video: {ex.Message}" });
            }
        }

        [HttpDelete("{movieId}")]
        public async Task<IActionResult> DeleteVideo(int movieId)
        {
            try
            {
                var movie = await _movieService.GetMovieByIdAsync(movieId);
                if (movie == null)
                {
                    return NotFound(new { message = "Movie not found" });
                }

                if (string.IsNullOrEmpty(movie.VideoFileName))
                {
                    return BadRequest(new { message = "Movie has no video file" });
                }

                // Delete all video files from Azure Blob (original + transcoded versions + HLS segments)
                
                // List all files that belong to this movie (prefix: movieId)
                var allMovieFiles = await _blobStorage.ListFilesAsync($"{movieId}");
                
                _logger.LogInformation($"Found {allMovieFiles.Count} files to delete for movie {movieId}");
                
                // Delete all files
                foreach (var fileName in allMovieFiles)
                {
                    await _blobStorage.DeleteVideoAsync(fileName);
                    _logger.LogDebug($"Deleted: {fileName}");
                }

                // Update database
                await _movieService.UpdateVideoFileNameAsync(movieId, null);

                _logger.LogInformation($"All {allMovieFiles.Count} video files deleted from Azure Blob for movie {movieId}");

                return Ok(new { message = "Video deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting video for movie {movieId}");
                return StatusCode(500, new { message = $"Error deleting video: {ex.Message}" });
            }
        }

        [HttpGet("{movieId}/info")]
        public async Task<IActionResult> GetVideoInfo(int movieId)
        {
            var movie = await _movieService.GetMovieByIdAsync(movieId);
            if (movie == null)
            {
                return NotFound(new { message = "Movie not found" });
            }

            if (string.IsNullOrEmpty(movie.VideoFileName))
            {
                return Ok(new { hasVideo = false });
            }

            // Check if HLS manifest exists (transcoding complete)
            var manifestFileName = $"{movieId}_master.m3u8";
            var manifestExists = await _blobStorage.VideoExistsAsync(manifestFileName);

            // Check original file
            var originalFileName = $"{movieId}.mp4";
            var originalExists = await _blobStorage.VideoExistsAsync(originalFileName);

            long fileSize = 0;
            if (originalExists)
            {
                fileSize = await _blobStorage.GetVideoSizeAsync(originalFileName);
            }

            // Check transcoded versions (HLS playlists or legacy MP4s)
            var transcodedVersions = new Dictionary<string, bool>();
            foreach (var resolution in new[] { "480p", "720p", "1080p" })
            {
                // Check for HLS playlist first, then fallback to MP4
                var hlsFileName = $"{movieId}_{resolution}.m3u8";
                var mp4FileName = $"{movieId}_{resolution}.mp4";
                var exists = await _blobStorage.VideoExistsAsync(hlsFileName) || 
                             await _blobStorage.VideoExistsAsync(mp4FileName);
                transcodedVersions[resolution] = exists;
            }

            return Ok(new
            {
                hasVideo = true,
                videoFileName = movie.VideoFileName,
                originalExists = originalExists,
                manifestExists = manifestExists,
                transcodingComplete = manifestExists,
                fileSizeMB = fileSize / 1024.0 / 1024.0,
                transcodedVersions = transcodedVersions,
                isHls = movie.VideoFileName.EndsWith("_master.m3u8")
            });
        }

        // Debug endpoint to list all files for a movie
        [HttpGet("{movieId}/files")]
        public async Task<IActionResult> ListMovieFiles(int movieId)
        {
            var files = await _blobStorage.ListFilesAsync($"{movieId}");
            return Ok(new { movieId, files });
        }
    }
}
