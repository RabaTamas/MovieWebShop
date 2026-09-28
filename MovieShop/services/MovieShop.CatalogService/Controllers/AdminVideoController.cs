using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.CatalogService.Services;

namespace MovieShop.CatalogService.Controllers;

/// <summary>
/// A monolit VideoController-ének megfelelője: azonos útvonalak, validációk,
/// válaszalakok (az AdminVideoUpload.jsx ezekre épít), és ugyanúgy Hangfire
/// háttérfeladatként indítja a transzkódolást.
/// </summary>
[ApiController]
// Az útvonal rögzített: a monolitban az osztály neve VideoController volt, így a
// [controller] token "Video" lett. Itt az osztálynév eltér, ezért explicit kell.
[Route("api/admin/Video")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminVideoController : ControllerBase
{
    private readonly IMovieService _movieService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<AdminVideoController> _logger;

    public AdminVideoController(
        IMovieService movieService,
        IBlobStorageService blobStorage,
        ILogger<AdminVideoController> logger)
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
            return BadRequest(new { message = "No video file provided" });

        var extension = Path.GetExtension(videoFile.FileName).ToLowerInvariant();
        if (extension != ".mp4")
            return BadRequest(new { message = "Only MP4 files are supported" });

        var movie = await _movieService.GetMovieByIdAsync(movieId);
        if (movie == null)
            return NotFound(new { message = "Movie not found" });

        try
        {
            var originalFileName = $"{movieId}.mp4";

            await using (var stream = videoFile.OpenReadStream())
            {
                await _blobStorage.UploadVideoAsync(stream, originalFileName);
            }

            _logger.LogInformation("Original video uploaded to Azure Blob for movie {MovieId}: {FileName}", movieId, originalFileName);

            // Azonnal elmentjük az eredeti MP4-et: a transzkódolás percekig tart, és addig a film
            // „videó nélkülinek" látszott (admin oldal üres, lejátszáskor a trailer jött be).
            // A /stream végpont az .mp4 fájlnévre az eredeti fájl SAS URL-jét adja; a transzkódolás
            // végén a TranscodingService a HLS mesterlistára cseréli.
            await _movieService.UpdateVideoFileNameAsync(movieId, originalFileName);

            // Hangfire háttérfeladat — a monolithoz hasonlóan túléli az újraindítást és újrapróbálkozik
            var jobId = BackgroundJob.Enqueue<ITranscodingService>(
                x => x.TranscodeVideoAsync(movieId, originalFileName));

            _logger.LogInformation("Transcoding job queued for movie {MovieId}, JobId: {JobId}", movieId, jobId);

            return Ok(new
            {
                message = "Video uploaded successfully. Transcoding started in background.",
                originalFileName,
                fileSize = videoFile.Length,
                movieId,
                jobId,
                status = "transcoding"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading video for movie {MovieId}", movieId);
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
                return NotFound(new { message = "Movie not found" });

            if (string.IsNullOrEmpty(movie.VideoFileName))
                return BadRequest(new { message = "Movie has no video file" });

            // Minden fájl törlése, ami a filmhez tartozik (eredeti, lejátszási listák, szegmensek)
            var allMovieFiles = await _blobStorage.ListFilesAsync($"{movieId}");

            foreach (var fileName in allMovieFiles)
                await _blobStorage.DeleteVideoAsync(fileName);

            await _movieService.UpdateVideoFileNameAsync(movieId, null);

            _logger.LogInformation("All {Count} video files deleted from Azure Blob for movie {MovieId}", allMovieFiles.Count, movieId);

            return Ok(new { message = "Video deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting video for movie {MovieId}", movieId);
            return StatusCode(500, new { message = $"Error deleting video: {ex.Message}" });
        }
    }

    [HttpGet("{movieId}/info")]
    public async Task<IActionResult> GetVideoInfo(int movieId)
    {
        var movie = await _movieService.GetMovieByIdAsync(movieId);
        if (movie == null)
            return NotFound(new { message = "Movie not found" });

        if (string.IsNullOrEmpty(movie.VideoFileName))
            return Ok(new { hasVideo = false });

        // A HLS mesterlista megléte jelzi, hogy a transzkódolás kész
        var manifestFileName = $"{movieId}_master.m3u8";
        var manifestExists = await _blobStorage.VideoExistsAsync(manifestFileName);

        var originalFileName = $"{movieId}.mp4";
        var originalExists = await _blobStorage.VideoExistsAsync(originalFileName);

        long fileSize = 0;
        if (originalExists)
            fileSize = await _blobStorage.GetVideoSizeAsync(originalFileName);

        // Átkódolt változatok (HLS lejátszási lista vagy régi MP4)
        var transcodedVersions = new Dictionary<string, bool>();
        foreach (var resolution in new[] { "480p", "720p", "1080p" })
        {
            var exists = await _blobStorage.VideoExistsAsync($"{movieId}_{resolution}.m3u8") ||
                         await _blobStorage.VideoExistsAsync($"{movieId}_{resolution}.mp4");
            transcodedVersions[resolution] = exists;
        }

        return Ok(new
        {
            hasVideo = true,
            videoFileName = movie.VideoFileName,
            originalExists,
            manifestExists,
            // Újrafeltöltésnél a régi mesterlista még létezik: csak akkor kész, ha a film már arra mutat
            transcodingComplete = manifestExists && movie.VideoFileName.EndsWith("_master.m3u8"),
            fileSizeMB = fileSize / 1024.0 / 1024.0,
            transcodedVersions,
            isHls = movie.VideoFileName.EndsWith("_master.m3u8")
        });
    }

    // Hibakereső végpont: a filmhez tartozó összes fájl listája
    [HttpGet("{movieId}/files")]
    public async Task<IActionResult> ListMovieFiles(int movieId)
    {
        var files = await _blobStorage.ListFilesAsync($"{movieId}");
        return Ok(new { movieId, files });
    }
}
