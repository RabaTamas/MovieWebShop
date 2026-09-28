using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Models;
using MovieShop.CatalogService.Services;
using MovieShop.ServiceDefaults;

namespace MovieShop.CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MovieController : ControllerBase
{
    private static readonly (string Name, int Height, string Bitrate)[] Resolutions =
    [
        ("480p", 480, "1000k"),
        ("720p", 720, "2500k"),
        ("1080p", 1080, "5000k")
    ];

    private readonly IMovieService _movieService;
    private readonly IEntitlementService _entitlements;
    private readonly ITmdbService _tmdbService;
    private readonly IBlobStorageService _blobStorage;
    private readonly CatalogDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// A böngésző felől látható API-cím. A gateway mögött a Request.Host a belső
    /// konténernév (catalogservice:8080) lenne, amit a HLS.js nem ér el — ezért a
    /// lejátszási listák URL-jeit a gateway nyilvános címéből építjük.
    /// </summary>
    private string PublicBaseUrl =>
        _configuration["PublicBaseUrl"]?.TrimEnd('/') ?? $"{Request.Scheme}://{Request.Host}";

    public MovieController(
        IMovieService movieService,
        IEntitlementService entitlements,
        ITmdbService tmdbService,
        IBlobStorageService blobStorage,
        CatalogDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _configuration = configuration;
        _movieService = movieService;
        _entitlements = entitlements;
        _tmdbService = tmdbService;
        _blobStorage = blobStorage;
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    // ── Publikus katalógus ────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MovieListDto>>> GetAllMovies()
        => Ok(await _movieService.GetAllMoviesAsync());

    /// <summary>
    /// Bővített TMDB-adatok (háttérképek, galéria, szereplők, rendező) a filmadatlaphoz.
    /// Nyilvános végpont: a képek és a stáb nem vásárláshoz kötött tartalom.
    /// </summary>
    [HttpGet("{id}/tmdb")]
    public async Task<ActionResult<TmdbMovieExtrasDto>> GetMovieTmdbExtras(int id)
    {
        var movie = await _movieService.GetMovieByIdAsync(id);
        if (movie == null)
            return NotFound(new { message = "Movie not found." });

        if (movie.TmdbInfo == null)
            return NotFound(new { message = "No TMDB data available for this movie." });

        var extras = await _tmdbService.GetMovieExtrasAsync(movie.TmdbInfo.TmdbId);
        return extras == null
            ? NotFound(new { message = "No TMDB data available for this movie." })
            : Ok(extras);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MovieDetailsWithTmdbDto>> GetMovie(int id)
    {
        var movie = await _movieService.GetMovieByIdAsync(id);
        return movie == null ? NotFound() : Ok(movie);
    }

    [HttpGet("category/{categoryId}")]
    public async Task<ActionResult<IEnumerable<MovieListDto>>> GetMoviesByCategory(int categoryId)
        => Ok(await _movieService.GetMoviesByCategoryAsync(categoryId));

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<MovieListDto>>> GetMoviesByCategories([FromQuery] List<int> categoryIds)
    {
        if (categoryIds == null || categoryIds.Count == 0)
            return BadRequest(new { message = "No category IDs provided." });

        return Ok(await _movieService.GetMoviesByCategoriesAsync(categoryIds));
    }

    // ── Adminisztráció ────────────────────────────────────────────────────────

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("admin/all")]
    public async Task<ActionResult<IEnumerable<MovieAdminListDto>>> GetAllMoviesForAdmin()
        => Ok(await _movieService.GetAllMoviesForAdminAsync());

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("admin/deleted")]
    public async Task<ActionResult<IEnumerable<MovieAdminListDto>>> GetDeletedMovies()
        => Ok(await _movieService.GetDeletedMoviesAsync());

    [Authorize(Policy = "RequireAdminRole")]
    [HttpGet("admin/{id}")]
    public async Task<ActionResult<MovieDetailsDto>> GetMovieForAdmin(int id)
    {
        var movie = await _movieService.GetMovieByIdForAdminAsync(id);
        return movie == null ? NotFound() : Ok(movie);
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost]
    public async Task<ActionResult> AddMovie(MovieDetailsDto movieDto)
    {
        if (!await _movieService.AddMovieAsync(movieDto))
            return BadRequest(new { message = "Failed to add movie" });

        return CreatedAtAction(nameof(GetMovie), new { id = movieDto.Id }, movieDto);
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateMovie(int id, MovieDetailsDto movieDto)
    {
        if (id != movieDto.Id)
            return BadRequest(new { message = "ID mismatch" });

        return await _movieService.UpdateMovieAsync(id, movieDto)
            ? NoContent()
            : NotFound(new { message = "Movie not found" });
    }

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteMovie(int id)
        => await _movieService.DeleteMovieAsync(id)
            ? Ok(new { message = "Movie deleted successfully" })
            : NotFound(new { message = "Movie not found" });

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPatch("{id}/restore")]
    public async Task<ActionResult> RestoreMovie(int id)
        => await _movieService.RestoreMovieAsync(id)
            ? Ok(new { message = "Movie restored successfully" })
            : NotFound(new { message = "Movie not found or not deleted" });

    [Authorize(Policy = "RequireAdminRole")]
    [HttpPost("{movieId}/category/{categoryId}")]
    public async Task<ActionResult> AddCategoryToMovie(int movieId, int categoryId)
        => await _movieService.AddCategoryToMovieAsync(movieId, categoryId)
            ? NoContent()
            : BadRequest(new { message = "Failed to add category to movie" });

    [Authorize(Policy = "RequireAdminRole")]
    [HttpDelete("{movieId}/category/{categoryId}")]
    public async Task<ActionResult> RemoveCategoryFromMovie(int movieId, int categoryId)
        => await _movieService.RemoveCategoryFromMovieAsync(movieId, categoryId)
            ? NoContent()
            : BadRequest(new { message = "Failed to remove category from movie" });

    // ── Megvásárolt tartalom és streaming ─────────────────────────────────────

    /// <summary>
    /// A monolitban ez `_orderService.GetPurchasedMovieIdsAsync(userId)` volt, vagyis
    /// egy join az Orders és OrderMovies táblákra. Itt a lokális Entitlements
    /// projekcióból olvasunk — az Order Service-t nem is kell elérni.
    /// </summary>
    [Authorize]
    [HttpGet("purchased")]
    public async Task<ActionResult<IEnumerable<MovieListDto>>> GetPurchasedMovies()
    {
        var movieIds = await _entitlements.GetEntitledMovieIdsAsync(User.GetUserId());

        if (movieIds.Count == 0)
            return Ok(new List<MovieListDto>());

        var allMovies = await _movieService.GetAllMoviesAsync();
        return Ok(allMovies.Where(m => movieIds.Contains(m.Id)));
    }

    [Authorize]
    [HttpGet("{id}/trailer")]
    public async Task<ActionResult<MovieTrailerDto>> GetMovieTrailer(int id)
    {
        if (!await _entitlements.HasAccessAsync(User.GetUserId(), id))
            return StatusCode(403, new MovieTrailerDto { Message = "You need to purchase this movie to watch it." });

        var movie = await _movieService.GetMovieByIdAsync(id);
        if (movie?.TmdbInfo?.TmdbId == null)
            return NotFound(new MovieTrailerDto { Message = "Movie not found or no TMDB data available." });

        try
        {
            var trailer = await _tmdbService.GetTrailerAsync(movie.TmdbInfo.TmdbId);
            if (trailer == null)
                return NotFound(new MovieTrailerDto { Message = "No trailer available for this movie." });

            return Ok(new MovieTrailerDto
            {
                YoutubeKey = trailer.Key,
                Url = $"https://www.youtube.com/embed/{trailer.Key}",
                Name = trailer.Name
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new MovieTrailerDto { Message = $"Error fetching trailer: {ex.Message}" });
        }
    }

    [Authorize]
    [HttpGet("{id}/stream")]
    public async Task<ActionResult> GetStreamingUrl(int id)
    {
        if (!await _entitlements.HasAccessAsync(User.GetUserId(), id))
            return StatusCode(403, new { message = "You need to purchase this movie to stream it." });

        var movie = await _movieService.GetMovieByIdAsync(id);
        if (movie == null)
            return NotFound(new { message = "Movie not found." });

        if (string.IsNullOrEmpty(movie.VideoFileName))
            return NotFound(new { message = "Video file not available for this movie." });

        try
        {
            if (movie.VideoFileName.EndsWith("_master.m3u8"))
            {
                if (!await _blobStorage.VideoExistsAsync(movie.VideoFileName))
                    return NotFound(new { message = "Video file not found in storage. Transcoding may still be in progress." });

                return Ok(new
                {
                    url = $"{PublicBaseUrl}/api/Movie/{id}/hls-master",
                    expiresAt = DateTime.UtcNow.AddHours(2),
                    movieTitle = movie.Title,
                    isHls = true
                });
            }

            // Visszafelé kompatibilitás: régi, MP4-alapú, több minőségű tartalom
            var qualities = new Dictionary<string, string>();

            foreach (var (quality, fileName) in new[]
                     {
                         ("480p", $"{id}_480p.mp4"),
                         ("720p", $"{id}_720p.mp4"),
                         ("1080p", $"{id}_1080p.mp4")
                     })
            {
                if (await _blobStorage.VideoExistsAsync(fileName))
                    qualities[quality] = await _blobStorage.GenerateSasUrlAsync(fileName, expiryHours: 1);
            }

            if (qualities.Count == 0)
            {
                var originalFileName = $"{id}.mp4";
                if (await _blobStorage.VideoExistsAsync(originalFileName))
                    qualities["original"] = await _blobStorage.GenerateSasUrlAsync(originalFileName, expiryHours: 1);
            }

            if (qualities.Count == 0)
                return NotFound(new { message = "Video file not found in storage. Transcoding may still be in progress." });

            var primaryQuality = qualities.ContainsKey("720p") ? "720p" : qualities.Keys.First();

            return Ok(new
            {
                url = qualities[primaryQuality],
                expiresAt = DateTime.UtcNow.AddHours(1),
                movieTitle = movie.Title,
                primaryQuality,
                availableQualities = qualities,
                isHls = false
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error generating streaming URL: {ex.Message}" });
        }
    }

    /// <summary>
    /// Dinamikus HLS mesterlejátszási lista. Minden minőségi szint a saját proxy
    /// végpontjára mutat, hogy a SAS URL-ek soha ne kerüljenek ki a kliensre statikusan.
    /// </summary>
    [Authorize]
    [HttpGet("{id}/hls-master")]
    public async Task<IActionResult> GetHlsMasterPlaylist(int id)
    {
        if (!await _entitlements.HasAccessAsync(User.GetUserId(), id))
            return StatusCode(403, new { message = "You need to purchase this movie to stream it." });

        var movie = await _movieService.GetMovieByIdAsync(id);
        if (movie == null || string.IsNullOrEmpty(movie.VideoFileName) || !movie.VideoFileName.EndsWith("_master.m3u8"))
            return NotFound(new { message = "HLS video not available." });

        var masterContent = "#EXTM3U\n#EXT-X-VERSION:3\n";
        var baseFileName = Path.GetFileNameWithoutExtension(movie.VideoFileName).Replace("_master", "");

        foreach (var resolution in Resolutions)
        {
            if (!await _blobStorage.VideoExistsAsync($"{baseFileName}_{resolution.Name}.m3u8"))
                continue;

            var proxyUrl = $"{PublicBaseUrl}/api/Movie/{id}/hls-quality/{resolution.Name}";
            var bandwidth = resolution.Bitrate.Replace("k", "000");
            var width = (int)Math.Round(resolution.Height * 16.0 / 9.0);

            masterContent += $"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={width}x{resolution.Height}\n";
            masterContent += $"{proxyUrl}\n";
        }

        return Content(masterContent, "application/vnd.apple.mpegurl");
    }

    [Authorize]
    [HttpGet("{id}/hls-quality/{quality}")]
    public async Task<IActionResult> GetHlsQualityPlaylist(int id, string quality)
    {
        if (!await _entitlements.HasAccessAsync(User.GetUserId(), id))
            return StatusCode(403, new { message = "You need to purchase this movie to stream it." });

        var movie = await _movieService.GetMovieByIdAsync(id);
        if (movie == null || string.IsNullOrEmpty(movie.VideoFileName))
            return NotFound(new { message = "Video not available." });

        try
        {
            var baseFileName = Path.GetFileNameWithoutExtension(movie.VideoFileName).Replace("_master", "");
            var playlistName = $"{baseFileName}_{quality}.m3u8";

            if (!await _blobStorage.VideoExistsAsync(playlistName))
                return NotFound(new { message = $"Quality {quality} not available." });

            var originalPlaylistUrl = await _blobStorage.GenerateSasUrlAsync(playlistName, expiryHours: 2);

            var httpClient = _httpClientFactory.CreateClient();
            var playlistContent = await httpClient.GetStringAsync(originalPlaylistUrl);

            // A szegmensek fájlnevét aláírt SAS URL-re cseréljük
            var modifiedContent = new System.Text.StringBuilder();

            foreach (var line in playlistContent.Split('\n'))
            {
                if (line.TrimEnd().EndsWith(".ts"))
                    modifiedContent.AppendLine(await _blobStorage.GenerateSasUrlAsync(line.Trim(), expiryHours: 2));
                else
                    modifiedContent.AppendLine(line);
            }

            return Content(modifiedContent.ToString(), "application/vnd.apple.mpegurl");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error generating quality playlist: {ex.Message}" });
        }
    }

    // ── Lejátszási pozíció ────────────────────────────────────────────────────

    [Authorize]
    [HttpGet("{id}/progress")]
    public async Task<ActionResult<VideoProgressDto>> GetProgress(int id)
    {
        var userId = User.GetUserId();

        var progress = await _context.VideoProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(vp => vp.UserId == userId && vp.MovieId == id);

        return Ok(new VideoProgressDto
        {
            ProgressSeconds = progress?.ProgressSeconds ?? 0,
            LastWatched = progress?.LastWatched ?? DateTime.UtcNow
        });
    }

    [Authorize]
    [HttpPost("{id}/progress")]
    public async Task<IActionResult> SaveProgress(int id, [FromBody] SaveProgressDto dto)
    {
        if (dto.ProgressSeconds < 0)
            return BadRequest(new { message = "Invalid progress value." });

        var userId = User.GetUserId();

        var progress = await _context.VideoProgresses
            .FirstOrDefaultAsync(vp => vp.UserId == userId && vp.MovieId == id);

        if (progress == null)
        {
            progress = new VideoProgress { UserId = userId, MovieId = id };
            _context.VideoProgresses.Add(progress);
        }

        progress.ProgressSeconds = dto.ProgressSeconds;
        progress.LastWatched = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "Progress saved." });
    }
}
