using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.CatalogService.Data;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Models;

namespace MovieShop.CatalogService.Services;

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync();
    Task<CategoryDto?> GetCategoryByIdAsync(int id);
    Task<bool> AddCategoryAsync(CategoryDto categoryDto);
    Task<bool> UpdateCategoryAsync(int id, CategoryDto categoryDto);
    Task<bool> DeleteCategoryAsync(int id);
    Task<bool> CategoryExistsAsync(int id);
    Task<int> GetMovieCountByCategoryAsync(int categoryId);
}

public class CategoryService : ICategoryService
{
    private readonly CatalogDbContext _context;
    private readonly IMapper _mapper;
    private readonly IMovieService _movieService;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        CatalogDbContext context,
        IMapper mapper,
        IMovieService movieService,
        ILogger<CategoryService> logger)
    {
        _context = context;
        _mapper = mapper;
        _movieService = movieService;
        _logger = logger;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        return _mapper.Map<IEnumerable<CategoryDto>>(categories);
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        return category == null ? null : _mapper.Map<CategoryDto>(category);
    }

    public async Task<bool> AddCategoryAsync(CategoryDto categoryDto)
    {
        try
        {
            var exists = await _context.Categories
                .AnyAsync(c => c.Name.ToLower() == categoryDto.Name.ToLower());

            if (exists)
            {
                _logger.LogWarning("A(z) '{CategoryName}' kategória már létezik", categoryDto.Name);
                return false;
            }

            var category = new Category { Name = categoryDto.Name.Trim() };

            await _context.Categories.AddAsync(category);
            await _context.SaveChangesAsync();

            categoryDto.Id = category.Id;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a(z) '{CategoryName}' kategória hozzáadásakor", categoryDto.Name);
            return false;
        }
    }

    /// <summary>
    /// A kategória átnevezése után minden érintett filmet újra kell publikálni,
    /// mert a MovieSnapshot kategórianeveket tárol, nem azonosítókat.
    /// </summary>
    public async Task<bool> UpdateCategoryAsync(int id, CategoryDto categoryDto)
    {
        try
        {
            var category = await _context.Categories
                .Include(c => c.Movies)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
            {
                _logger.LogWarning("A(z) {CategoryId} azonosítójú kategória nem található", id);
                return false;
            }

            var nameTaken = await _context.Categories
                .AnyAsync(c => c.Name.ToLower() == categoryDto.Name.ToLower() && c.Id != id);

            if (nameTaken)
            {
                _logger.LogWarning("Már létezik '{CategoryName}' nevű kategória", categoryDto.Name);
                return false;
            }

            var affectedMovieIds = category.Movies.Select(m => m.Id).ToList();

            category.Name = categoryDto.Name.Trim();
            await _context.SaveChangesAsync();

            foreach (var movieId in affectedMovieIds)
                await _movieService.PublishMovieChangedAsync(movieId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a(z) {CategoryId} kategória módosításakor", id);
            return false;
        }
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        try
        {
            var category = await _context.Categories
                .Include(c => c.Movies)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return false;

            var affectedMovieIds = category.Movies.Select(m => m.Id).ToList();

            // Csak a kapcsolat szűnik meg, a filmek maradnak
            foreach (var movie in category.Movies.ToList())
                movie.Categories.Remove(category);

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            foreach (var movieId in affectedMovieIds)
                await _movieService.PublishMovieChangedAsync(movieId);

            _logger.LogInformation("A(z) {CategoryId} kategória törölve", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hiba a(z) {CategoryId} kategória törlésekor", id);
            return false;
        }
    }

    public Task<bool> CategoryExistsAsync(int id)
        => _context.Categories.AnyAsync(c => c.Id == id);

    public async Task<int> GetMovieCountByCategoryAsync(int categoryId)
        => await _context.Movies.CountAsync(m => m.Categories.Any(c => c.Id == categoryId));
}
