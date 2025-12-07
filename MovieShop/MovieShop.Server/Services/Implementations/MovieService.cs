using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.DTOs.TMDB;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;
using MovieShop.Server.Services.Interfaces.TMDB;

namespace MovieShop.Server.Services.Implementations
{
    public class MovieService : IMovieService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly ITmdbService _tmdbService;

        public MovieService(AppDbContext context, IMapper mapper, ITmdbService tmdbService)
        {
            _context = context;
            _mapper = mapper;
            _tmdbService = tmdbService;
        }

        public async Task<IEnumerable<MovieListDto>> GetAllMoviesAsync()
        {
            var movies = await _context.Movies
                .Where(m => !m.IsDeleted)
                .AsNoTracking()
                .ToListAsync();

            return _mapper.Map<IEnumerable<MovieListDto>>(movies);
        }

        public async Task<IEnumerable<MovieAdminListDto>> GetAllMoviesForAdminAsync()
        {
            var movies = await _context.Movies
                .IgnoreQueryFilters() // This bypasses the global filter
                .AsNoTracking()
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return _mapper.Map<IEnumerable<MovieAdminListDto>>(movies);
        }

        // Get only deleted movies (for admin)
        public async Task<IEnumerable<MovieAdminListDto>> GetDeletedMoviesAsync()
        {
            var movies = await _context.Movies
                .IgnoreQueryFilters()
                .Where(m => m.IsDeleted)
                .AsNoTracking()
                .OrderByDescending(m => m.DeletedAt)
                .ToListAsync();

            return _mapper.Map<IEnumerable<MovieAdminListDto>>(movies);
        }


        public async Task<MovieDetailsWithTmdbDto?> GetMovieByIdAsync(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.Categories)
                .Include(m => m.Reviews)
                    .ThenInclude(r => r.User)
                .Where(m => !m.IsDeleted)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (movie == null)
                return null;

            var movieDto = _mapper.Map<MovieDetailsWithTmdbDto>(movie);

            // Try to get TMDB data
            var tmdbMovie = await _tmdbService.SearchMovieAsync(movie.Title);
            if (tmdbMovie != null)
            {
                movieDto.TmdbInfo = new TmdbMovieInfo
                {
                    TmdbId = tmdbMovie.Id,
                    VoteAverage = tmdbMovie.VoteAverage,
                    VoteCount = tmdbMovie.VoteCount,
                    ReleaseDate = tmdbMovie.ReleaseDate
                };
            }

            return movieDto;
        }

        public async Task<MovieDetailsDto?> GetMovieByIdForAdminAsync(int id)
        {
            var movie = await _context.Movies
                .IgnoreQueryFilters()
                .Include(m => m.Categories)
                .Include(m => m.Reviews)
                    .ThenInclude(r => r.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            return movie == null ? null : _mapper.Map<MovieDetailsDto>(movie);
        }

        public async Task<IEnumerable<MovieListDto>> GetMoviesByCategoryAsync(int categoryId)
        {
            var movies = await _context.Movies
                .Include(m => m.Categories)
                .Where(m => !m.IsDeleted && m.Categories.Any(c => c.Id == categoryId))
                .AsNoTracking()
                .ToListAsync();

            return _mapper.Map<IEnumerable<MovieListDto>>(movies);
        }

        public async Task<IEnumerable<MovieListDto>> GetMoviesByCategoriesAsync(IEnumerable<int> categoryIds)
        {
            if (categoryIds == null || !categoryIds.Any())
            {
                return Enumerable.Empty<MovieListDto>();
            }

            var movies = await _context.Movies
                .Include(m => m.Categories)
                .Where(m => !m.IsDeleted && m.Categories.Any(c => categoryIds.Contains(c.Id)))
                .AsNoTracking()
                .ToListAsync();

            return _mapper.Map<IEnumerable<MovieListDto>>(movies);
        }

        public async Task<bool> AddMovieAsync(MovieDetailsDto movieDto)
        {
            try
            {
                var movie = new Movie
                {
                    Title = movieDto.Title,
                    Description = movieDto.Description,
                    Price = movieDto.Price,
                    DiscountedPrice = movieDto.DiscountedPrice,
                    ImageUrl = movieDto.ImageUrl
                };

                if (movieDto.Categories != null && movieDto.Categories.Any())
                {
                    var categoryIds = movieDto.Categories.Select(c => c.Id);
                    var existingCategories = await _context.Categories
                        .Where(c => categoryIds.Contains(c.Id))
                        .ToListAsync();

                    foreach (var category in existingCategories)
                    {
                        movie.Categories.Add(category);
                    }
                }

                await _context.Movies.AddAsync(movie);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateMovieAsync(int id, MovieDetailsDto movieDto)
        {
            try
            {
                var movie = await _context.Movies
                    .Include(m => m.Categories)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (movie == null)
                    return false;

                // Update basic properties
                movie.Title = movieDto.Title;
                movie.Description = movieDto.Description;
                movie.Price = movieDto.Price;
                movie.DiscountedPrice = movieDto.DiscountedPrice;
                movie.ImageUrl = movieDto.ImageUrl;

                // Update categories if provided
                if (movieDto.Categories != null)
                {
                    movie.Categories.Clear();
                    var categoryIds = movieDto.Categories.Select(c => c.Id);
                    var existingCategories = await _context.Categories
                        .Where(c => categoryIds.Contains(c.Id))
                        .ToListAsync();

                    foreach (var category in existingCategories)
                    {
                        movie.Categories.Add(category);
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Soft delete a movie
        public async Task<bool> DeleteMovieAsync(int id)
        {
            try
            {
                var movie = await _context.Movies
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (movie == null)
                    return false;

                movie.IsDeleted = true;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Restore a soft-deleted movie
        public async Task<bool> RestoreMovieAsync(int id)
        {
            try
            {
                var movie = await _context.Movies
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(m => m.Id == id && m.IsDeleted);

                if (movie == null)
                    return false;

                movie.IsDeleted = false;
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }


        public async Task<bool> AddCategoryToMovieAsync(int movieId, int categoryId)
        {
            try
            {
                var movie = await _context.Movies
                    .Include(m => m.Categories)
                    .FirstOrDefaultAsync(m => m.Id == movieId);

                var category = await _context.Categories.FindAsync(categoryId);

                if (movie == null || category == null)
                    return false;

                if (!movie.Categories.Any(c => c.Id == categoryId))
                {
                    movie.Categories.Add(category);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveCategoryFromMovieAsync(int movieId, int categoryId)
        {
            try
            {
                var movie = await _context.Movies
                    .Include(m => m.Categories)
                    .FirstOrDefaultAsync(m => m.Id == movieId);

                if (movie == null)
                    return false;

                var category = movie.Categories.FirstOrDefault(c => c.Id == categoryId);
                if (category != null)
                {
                    movie.Categories.Remove(category);
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task UpdateVideoFileNameAsync(int movieId, string? videoFileName)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie != null)
            {
                movie.VideoFileName = videoFileName;
                await _context.SaveChangesAsync();
            }
        }
    }
}
