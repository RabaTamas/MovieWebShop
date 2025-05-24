using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.DTOs.TMDB;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MovieController : ControllerBase
    {
        private readonly IMovieService _movieService;

        public MovieController(IMovieService movieService)
        {
            _movieService = movieService;
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
    }
}
