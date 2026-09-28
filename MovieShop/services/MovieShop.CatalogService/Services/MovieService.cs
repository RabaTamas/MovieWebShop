using AutoMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Models;
using MovieShop.Contracts.Events;

namespace MovieShop.CatalogService.Services;

public interface IMovieService
{
    Task<IEnumerable<MovieListDto>> GetAllMoviesAsync();
    Task<IEnumerable<MovieAdminListDto>> GetAllMoviesForAdminAsync();
    Task<IEnumerable<MovieAdminListDto>> GetDeletedMoviesAsync();
    Task<MovieDetailsWithTmdbDto?> GetMovieByIdAsync(int id);
    Task<MovieDetailsDto?> GetMovieByIdForAdminAsync(int id);
    Task<IEnumerable<MovieListDto>> GetMoviesByCategoryAsync(int categoryId);
    Task<IEnumerable<MovieListDto>> GetMoviesByCategoriesAsync(IEnumerable<int> categoryIds);
    Task<bool> AddMovieAsync(MovieDetailsDto movieDto);
    Task<bool> UpdateMovieAsync(int id, MovieDetailsDto movieDto);
    Task<bool> DeleteMovieAsync(int id);
    Task<bool> RestoreMovieAsync(int id);
    Task<bool> AddCategoryToMovieAsync(int movieId, int categoryId);
    Task<bool> RemoveCategoryFromMovieAsync(int movieId, int categoryId);
    Task UpdateVideoFileNameAsync(int movieId, string? videoFileName);
    Task PublishMovieChangedAsync(int movieId, bool isNew = false);
}

public class MovieService : IMovieService
{
    private readonly CatalogDbContext _context;
    private readonly IMapper _mapper;
    private readonly ITmdbService _tmdbService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MovieService> _logger;

    public MovieService(
        CatalogDbContext context,
        IMapper mapper,
        ITmdbService tmdbService,
        IPublishEndpoint publishEndpoint,
        ILogger<MovieService> logger)
    {
        _context = context;
        _mapper = mapper;
        _tmdbService = tmdbService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
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
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        return _mapper.Map<IEnumerable<MovieAdminListDto>>(movies);
    }

    public async Task<IEnumerable<MovieAdminListDto>> GetDeletedMoviesAsync()
    {
        var movies = await _context.Movies
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
            .Where(m => !m.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return null;

        var movieDto = _mapper.Map<MovieDetailsWithTmdbDto>(movie);
        movieDto.Reviews = await LoadReviewsWithAuthorsAsync(movie.Id, movie.Title);

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
            .Include(m => m.Categories)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return null;

        var dto = _mapper.Map<MovieDetailsDto>(movie);
        dto.Reviews = await LoadReviewsWithAuthorsAsync(movie.Id, movie.Title);
        return dto;
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
        var ids = categoryIds?.ToList() ?? [];
        if (ids.Count == 0)
            return [];

        var movies = await _context.Movies
            .Include(m => m.Categories)
            .Where(m => !m.IsDeleted && m.Categories.Any(c => ids.Contains(c.Id)))
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

            if (movieDto.Categories?.Count > 0)
            {
                var categoryIds = movieDto.Categories.Select(c => c.Id).ToList();
                var existingCategories = await _context.Categories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToListAsync();

                movie.Categories.AddRange(existingCategories);
            }

            await _context.Movies.AddAsync(movie);
            await _context.SaveChangesAsync();

            movieDto.Id = movie.Id;

            // Az Order és Auction Service ebből frissíti a MovieSnapshot tábláját;
            // az IsNew jelzés alapján a User Service „új film" push értesítést küld
            await PublishMovieChangedAsync(movie.Id, isNew: true);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a film hozzáadásakor: {Title}", movieDto.Title);
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

            movie.Title = movieDto.Title;
            movie.Description = movieDto.Description;
            movie.Price = movieDto.Price;
            movie.DiscountedPrice = movieDto.DiscountedPrice;
            movie.ImageUrl = movieDto.ImageUrl;

            if (movieDto.Categories != null)
            {
                movie.Categories.Clear();
                var categoryIds = movieDto.Categories.Select(c => c.Id).ToList();
                var existingCategories = await _context.Categories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToListAsync();

                movie.Categories.AddRange(existingCategories);
            }

            await _context.SaveChangesAsync();
            await PublishMovieChangedAsync(movie.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a(z) {MovieId} film módosításakor", id);
            return false;
        }
    }

    public async Task<bool> DeleteMovieAsync(int id)
    {
        try
        {
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id);
            if (movie == null)
                return false;

            movie.IsDeleted = true;
            await _context.SaveChangesAsync();

            // A törlést is publikálni kell: a kosarakban lévő film sem vásárolható tovább
            await PublishMovieChangedAsync(movie.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a(z) {MovieId} film törlésekor", id);
            return false;
        }
    }

    public async Task<bool> RestoreMovieAsync(int id)
    {
        try
        {
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.Id == id && m.IsDeleted);
            if (movie == null)
                return false;

            movie.IsDeleted = false;
            await _context.SaveChangesAsync();
            await PublishMovieChangedAsync(movie.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a(z) {MovieId} film visszaállításakor", id);
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

            if (movie.Categories.All(c => c.Id != categoryId))
            {
                movie.Categories.Add(category);
                await _context.SaveChangesAsync();
                await PublishMovieChangedAsync(movieId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a kategória hozzáadásakor a(z) {MovieId} filmhez", movieId);
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
                await PublishMovieChangedAsync(movieId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a kategória eltávolításakor a(z) {MovieId} filmből", movieId);
            return false;
        }
    }

    public async Task UpdateVideoFileNameAsync(int movieId, string? videoFileName)
    {
        var movie = await _context.Movies.FindAsync(movieId);
        if (movie == null)
            return;

        movie.VideoFileName = videoFileName;
        await _context.SaveChangesAsync();
        await PublishMovieChangedAsync(movieId);
    }

    /// <summary>
    /// A film aktuális állapotának közzététele az üzenetsoron. Minden írási művelet
    /// meghívja, így a többi service snapshotja soha nem marad le tartósan.
    /// </summary>
    public async Task PublishMovieChangedAsync(int movieId, bool isNew = false)
    {
        var movie = await _context.Movies
            .Include(m => m.Categories)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == movieId);

        if (movie == null)
            return;

        await _publishEndpoint.Publish(new MovieChanged
        {
            MovieId = movie.Id,
            Title = movie.Title,
            Description = movie.Description,
            ImageUrl = movie.ImageUrl,
            Price = movie.Price,
            DiscountedPrice = movie.DiscountedPrice,
            Categories = movie.Categories.Select(c => c.Name).ToList(),
            IsDeleted = movie.IsDeleted,
            VideoFileName = movie.VideoFileName,
            IsNew = isNew
        });
    }

    /// <summary>
    /// Értékelések betöltése a szerzők nevével. A név a lokális UserSnapshot
    /// táblából jön — a monolitban ez `Include(r =&gt; r.User)` volt.
    /// </summary>
    private async Task<List<ReviewDto>> LoadReviewsWithAuthorsAsync(int movieId, string movieTitle)
    {
        return await _context.Reviews
            .Where(r => r.MovieId == movieId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                Content = r.Content,
                UserId = r.UserId,
                MovieId = r.MovieId,
                UserName = _context.UserSnapshots
                    .Where(u => u.UserId == r.UserId)
                    .Select(u => u.UserName)
                    .FirstOrDefault() ?? "Unknown user",
                User = new UserDto
                {
                    Id = r.UserId,
                    Name = _context.UserSnapshots.Where(u => u.UserId == r.UserId).Select(u => u.UserName).FirstOrDefault() ?? "Unknown user",
                    Email = _context.UserSnapshots.Where(u => u.UserId == r.UserId).Select(u => u.Email).FirstOrDefault() ?? "",
                    Role = ""
                },
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();
    }
}
