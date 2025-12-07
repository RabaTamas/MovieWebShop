using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.DTOs.TMDB;
using MovieShop.Server.Services;
using MovieShop.Server.Services.Interfaces;
using System.Security.Claims;
using System.Text.Json;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MovieController : ControllerBase
    {
        private readonly IMovieService _movieService;
        private readonly IOrderService _orderService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IStreamingService _streamingService;
        private readonly IBlobStorageService _blobStorage;

        public MovieController(
            IMovieService movieService,
            IOrderService orderService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IStreamingService streamingService,
            IBlobStorageService blobStorage)
        {
            _movieService = movieService;
            _orderService = orderService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _streamingService = streamingService;
            _blobStorage = blobStorage;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MovieListDto>>> GetAllMovies()
        {
            var movies = await _movieService.GetAllMoviesAsync();
            return Ok(movies);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MovieDetailsWithTmdbDto>> GetMovie(int id)
        {
            var movie = await _movieService.GetMovieByIdAsync(id);

            if (movie == null)
                return NotFound();

            return Ok(movie);
        }

        [HttpGet("category/{categoryId}")]
        public async Task<ActionResult<IEnumerable<MovieListDto>>> GetMoviesByCategory(int categoryId)
        {
            var movies = await _movieService.GetMoviesByCategoryAsync(categoryId);
            return Ok(movies);
        }

        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<MovieListDto>>> GetMoviesByCategories([FromQuery] List<int> categoryIds)
        {
            if (categoryIds == null || categoryIds.Count == 0)
                return BadRequest(new { message = "No category IDs provided." });

            var movies = await _movieService.GetMoviesByCategoriesAsync(categoryIds);
            return Ok(movies);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("admin/all")]
        public async Task<ActionResult<IEnumerable<MovieAdminListDto>>> GetAllMoviesForAdmin()
        {
            var movies = await _movieService.GetAllMoviesForAdminAsync();
            return Ok(movies);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("admin/deleted")]
        public async Task<ActionResult<IEnumerable<MovieAdminListDto>>> GetDeletedMovies()
        {
            var movies = await _movieService.GetDeletedMoviesAsync();
            return Ok(movies);
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("admin/{id}")]
        public async Task<ActionResult<MovieDetailsDto>> GetMovieForAdmin(int id)
        {
            var movie = await _movieService.GetMovieByIdForAdminAsync(id);

            if (movie == null)
                return NotFound();

            return Ok(movie);
        }


        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost]
        public async Task<ActionResult> AddMovie(MovieDetailsDto movieDto)
        {
            var result = await _movieService.AddMovieAsync(movieDto);

            if (!result)
                return BadRequest(new { message = "Failed to add movie" });

            return CreatedAtAction(nameof(GetMovie), new { id = movieDto.Id }, movieDto);
        }
        
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateMovie(int id, MovieDetailsDto movieDto)
        {
            if (id != movieDto.Id)
                return BadRequest(new { message = "ID mismatch" });

            var result = await _movieService.UpdateMovieAsync(id, movieDto);

            if (!result)
                return NotFound(new { message = "Movie not found" });

            return NoContent();
        }

        /*[Authorize(Policy = "RequireAdminRole")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteMovie(int id)
        {
            Console.WriteLine($"Attempting to delete movie with ID: {id}");

            var result = await _movieService.DeleteMovieAsync(id);

            if (!result)
            {
                Console.WriteLine($"Movie with ID {id} not found in service");
                return NotFound(new { message = "Movie not found" });
            }

            Console.WriteLine($"Movie with ID {id} deleted successfully");
            return NoContent();
        }*/

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("{movieId}/category/{categoryId}")]
        public async Task<ActionResult> AddCategoryToMovie(int movieId, int categoryId)
        {
            var result = await _movieService.AddCategoryToMovieAsync(movieId, categoryId);

            if (!result)
                return BadRequest(new { message = "Failed to add category to movie" });

            return NoContent();
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpDelete("{movieId}/category/{categoryId}")]
        public async Task<ActionResult> RemoveCategoryFromMovie(int movieId, int categoryId)
        {
            var result = await _movieService.RemoveCategoryFromMovieAsync(movieId, categoryId);

            if (!result)
                return BadRequest(new { message = "Failed to remove category from movie" });

            return NoContent();
        }


        // Soft delete
        [Authorize(Policy = "RequireAdminRole")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteMovie(int id)
        {
            var result = await _movieService.DeleteMovieAsync(id);

            if (!result)
                return NotFound(new { message = "Movie not found" });

            return Ok(new { message = "Movie deleted successfully" });
        }

        // Restore soft-deleted movie
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPatch("{id}/restore")]
        public async Task<ActionResult> RestoreMovie(int id)
        {
            var result = await _movieService.RestoreMovieAsync(id);

            if (!result)
                return NotFound(new { message = "Movie not found or not deleted" });

            return Ok(new { message = "Movie restored successfully" });
        }

        // Get purchased movies for current user
        [Authorize]
        [HttpGet("purchased")]
        public async Task<ActionResult<IEnumerable<MovieListDto>>> GetPurchasedMovies()
        {
            var userId = GetCurrentUserId();
            var movieIds = await _orderService.GetPurchasedMovieIdsAsync(userId);
            
            if (!movieIds.Any())
                return Ok(new List<MovieListDto>());

            var allMovies = await _movieService.GetAllMoviesAsync();
            var purchasedMovies = allMovies.Where(m => movieIds.Contains(m.Id));
            
            return Ok(purchasedMovies);
        }

        // Get movie trailer (TMDB)
        [Authorize]
        [HttpGet("{id}/trailer")]
        public async Task<ActionResult<MovieTrailerDto>> GetMovieTrailer(int id)
        {
            var userId = GetCurrentUserId();
            
            // Check if user has purchased the movie
            var hasPurchased = await _orderService.HasUserPurchasedMovieAsync(userId, id);
            if (!hasPurchased)
            {
                return StatusCode(403, new MovieTrailerDto 
                { 
                    Message = "You need to purchase this movie to watch it." 
                });
            }

            // Get TMDB ID from movie
            var movie = await _movieService.GetMovieByIdAsync(id);
            if (movie == null || movie.TmdbInfo?.TmdbId == null)
            {
                return NotFound(new MovieTrailerDto 
                { 
                    Message = "Movie not found or no TMDB data available." 
                });
            }

            // Fetch trailer from TMDB API
            try
            {
                var tmdbApiKey = _configuration["TmdbApi:ApiKey"];
                var httpClient = _httpClientFactory.CreateClient();
                
                var url = $"https://api.themoviedb.org/3/movie/{movie.TmdbInfo.TmdbId}/videos?api_key={tmdbApiKey}";
                var response = await httpClient.GetStringAsync(url);
                
                var videoResponse = JsonSerializer.Deserialize<TmdbVideoResponseDto>(response, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                // Find first YouTube trailer
                var trailer = videoResponse?.Results?
                    .FirstOrDefault(v => v.Type.Equals("Trailer", StringComparison.OrdinalIgnoreCase) 
                                      && v.Site.Equals("YouTube", StringComparison.OrdinalIgnoreCase));

                if (trailer == null)
                {
                    return NotFound(new MovieTrailerDto 
                    { 
                        Message = "No trailer available for this movie." 
                    });
                }

                return Ok(new MovieTrailerDto
                {
                    YoutubeKey = trailer.Key,
                    Url = $"https://www.youtube.com/embed/{trailer.Key}",
                    Name = trailer.Name
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new MovieTrailerDto 
                { 
                    Message = $"Error fetching trailer: {ex.Message}" 
                });
            }
        }

        // Get streaming URL for purchased movie
        [Authorize]
        [HttpGet("{id}/stream")]
        public async Task<ActionResult<StreamingUrlDto>> GetStreamingUrl(int id)
        {
            var userId = GetCurrentUserId();
            
            // Check if user has purchased the movie
            var hasPurchased = await _orderService.HasUserPurchasedMovieAsync(userId, id);
            if (!hasPurchased)
            {
                return StatusCode(403, new { message = "You need to purchase this movie to stream it." });
            }

            // Get movie with video file name
            var movie = await _movieService.GetMovieByIdAsync(id);
            if (movie == null)
            {
                return NotFound(new { message = "Movie not found." });
            }

            if (string.IsNullOrEmpty(movie.VideoFileName))
            {
                return NotFound(new { message = "Video file not available for this movie." });
            }

            // Generate SAS URL for HLS master playlist
            try
            {
                // Check if master playlist exists (HLS format)
                if (movie.VideoFileName.EndsWith("_master.m3u8"))
                {
                    if (!await _blobStorage.VideoExistsAsync(movie.VideoFileName))
                    {
                        return NotFound(new { message = "Video file not found in storage. Transcoding may still be in progress." });
                    }

                    // Return URL to dynamic master playlist endpoint (includes auth)
                    var dynamicMasterUrl = $"{Request.Scheme}://{Request.Host}/api/Movie/{id}/hls-master";
                    
                    return Ok(new 
                    {
                        url = dynamicMasterUrl,
                        expiresAt = DateTime.UtcNow.AddHours(2),
                        movieTitle = movie.Title,
                        isHls = true
                    });
                }
                
                // Fallback: Legacy MP4 multi-quality support
                var qualities = new Dictionary<string, string>();
                var qualityOptions = new[] 
                { 
                    ("480p", $"{id}_480p.mp4"),
                    ("720p", $"{id}_720p.mp4"),
                    ("1080p", $"{id}_1080p.mp4")
                };

                foreach (var (quality, fileName) in qualityOptions)
                {
                    if (await _blobStorage.VideoExistsAsync(fileName))
                    {
                        var sasUrl = await _blobStorage.GenerateSasUrlAsync(fileName, expiryHours: 1);
                        qualities[quality] = sasUrl;
                    }
                }

                // If no transcoded versions exist, use original
                if (qualities.Count == 0)
                {
                    var originalFileName = $"{id}.mp4";
                    if (await _blobStorage.VideoExistsAsync(originalFileName))
                    {
                        var sasUrl = await _blobStorage.GenerateSasUrlAsync(originalFileName, expiryHours: 1);
                        qualities["original"] = sasUrl;
                    }
                }

                if (qualities.Count == 0)
                {
                    return NotFound(new { message = "Video file not found in storage. Transcoding may still be in progress." });
                }

                // Return primary URL (highest available quality) + all alternatives
                var primaryQuality = qualities.ContainsKey("720p") ? "720p" : qualities.Keys.First();
                var primaryUrl = qualities[primaryQuality];

                return Ok(new 
                {
                    url = primaryUrl,
                    expiresAt = DateTime.UtcNow.AddHours(1),
                    movieTitle = movie.Title,
                    primaryQuality = primaryQuality,
                    availableQualities = qualities,
                    isHls = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error generating streaming URL: {ex.Message}" });
            }
        }

        // Dynamic HLS master playlist with full SAS URLs
        [Authorize]
        [HttpGet("{id}/hls-master")]
        public async Task<IActionResult> GetHlsMasterPlaylist(int id)
        {
            var userId = GetCurrentUserId();
            
            // Check if user has purchased the movie
            var hasPurchased = await _orderService.HasUserPurchasedMovieAsync(userId, id);
            if (!hasPurchased)
            {
                return StatusCode(403, new { message = "You need to purchase this movie to stream it." });
            }

            var movie = await _movieService.GetMovieByIdAsync(id);
            if (movie == null || string.IsNullOrEmpty(movie.VideoFileName) || !movie.VideoFileName.EndsWith("_master.m3u8"))
            {
                return NotFound(new { message = "HLS video not available." });
            }

            try
            {
                // Generate master playlist with proxy URLs for quality playlists
                var resolutions = new (string Name, int Height, string Bitrate)[]
                {
                    ("480p", 480, "1000k"),
                    ("720p", 720, "2500k"),
                    ("1080p", 1080, "5000k")
                };

                var masterContent = "#EXTM3U\n#EXT-X-VERSION:3\n";
                var baseFileName = Path.GetFileNameWithoutExtension(movie.VideoFileName).Replace("_master", "");

                foreach (var resolution in resolutions)
                {
                    var playlistName = $"{baseFileName}_{resolution.Name}.m3u8";
                    
                    if (await _blobStorage.VideoExistsAsync(playlistName))
                    {
                        // Use proxy URL instead of direct SAS URL
                        var proxyUrl = $"{Request.Scheme}://{Request.Host}/api/Movie/{id}/hls-quality/{resolution.Name}";
                        var bandwidth = resolution.Bitrate.Replace("k", "000");
                        var width = (int)Math.Round(resolution.Height * 16.0 / 9.0);
                        
                        masterContent += $"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={width}x{resolution.Height}\n";
                        masterContent += $"{proxyUrl}\n";
                    }
                }

                return Content(masterContent, "application/vnd.apple.mpegurl");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error generating HLS master playlist: {ex.Message}" });
            }
        }

        // Dynamic HLS quality playlist with SAS URLs for segments
        [Authorize]
        [HttpGet("{id}/hls-quality/{quality}")]
        public async Task<IActionResult> GetHlsQualityPlaylist(int id, string quality)
        {
            var userId = GetCurrentUserId();
            
            // Check if user has purchased the movie
            var hasPurchased = await _orderService.HasUserPurchasedMovieAsync(userId, id);
            if (!hasPurchased)
            {
                return StatusCode(403, new { message = "You need to purchase this movie to stream it." });
            }

            var movie = await _movieService.GetMovieByIdAsync(id);
            if (movie == null || string.IsNullOrEmpty(movie.VideoFileName))
            {
                return NotFound(new { message = "Video not available." });
            }

            try
            {
                var baseFileName = Path.GetFileNameWithoutExtension(movie.VideoFileName).Replace("_master", "");
                var playlistName = $"{baseFileName}_{quality}.m3u8";
                
                if (!await _blobStorage.VideoExistsAsync(playlistName))
                {
                    return NotFound(new { message = $"Quality {quality} not available." });
                }

                // Download the original playlist from Azure Blob
                var originalPlaylistUrl = await _blobStorage.GenerateSasUrlAsync(playlistName, expiryHours: 2);
                using var httpClient = new HttpClient();
                var playlistContent = await httpClient.GetStringAsync(originalPlaylistUrl);

                // Replace segment filenames with SAS URLs
                var lines = playlistContent.Split('\n');
                var modifiedContent = new System.Text.StringBuilder();

                foreach (var line in lines)
                {
                    if (line.EndsWith(".ts"))
                    {
                        // This is a segment filename - generate SAS URL
                        var segmentFileName = line.Trim();
                        var segmentSasUrl = await _blobStorage.GenerateSasUrlAsync(segmentFileName, expiryHours: 2);
                        modifiedContent.AppendLine(segmentSasUrl);
                    }
                    else
                    {
                        modifiedContent.AppendLine(line);
                    }
                }

                return Content(modifiedContent.ToString(), "application/vnd.apple.mpegurl");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error generating quality playlist: {ex.Message}" });
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim ?? "0");
        }
    }
}
