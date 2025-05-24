using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieShop.Server.Data;
using MovieShop.Server.DTOs;
using MovieShop.Server.Models;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Services.Implementations
{
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(AppDbContext context, IMapper mapper, ILogger<CategoryService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync()
        {
            try
            {
                var categories = await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<CategoryDto>>(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching all categories");
                throw;
            }
        }

        public async Task<CategoryDto?> GetCategoryByIdAsync(int id)
        {
            try
            {
                var category = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

                return category == null ? null : _mapper.Map<CategoryDto>(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching category with ID {CategoryId}", id);
                throw;
            }
        }

        public async Task<bool> AddCategoryAsync(CategoryDto categoryDto)
        {
            try
            {
                // Check if category with same name already exists
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == categoryDto.Name.ToLower());

                if (existingCategory != null)
                {
                    _logger.LogWarning("Category with name '{CategoryName}' already exists", categoryDto.Name);
                    return false;
                }

                var category = new Category
                {
                    Name = categoryDto.Name.Trim()
                };

                await _context.Categories.AddAsync(category);
                await _context.SaveChangesAsync();

                // Update the DTO with the new ID
                categoryDto.Id = category.Id;

                _logger.LogInformation("Successfully added category with ID {CategoryId}", category.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while adding category '{CategoryName}'", categoryDto.Name);
                return false;
            }
        }

        public async Task<bool> UpdateCategoryAsync(int id, CategoryDto categoryDto)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);

                if (category == null)
                {
                    _logger.LogWarning("Category with ID {CategoryId} not found for update", id);
                    return false;
                }

                // Check if another category with the same name exists (excluding current category)
                var existingCategory = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == categoryDto.Name.ToLower() && c.Id != id);

                if (existingCategory != null)
                {
                    _logger.LogWarning("Another category with name '{CategoryName}' already exists", categoryDto.Name);
                    return false;
                }

                category.Name = categoryDto.Name.Trim();

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated category with ID {CategoryId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating category with ID {CategoryId}", id);
                return false;
            }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Movies) // Include related movies to check relationships
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                {
                    _logger.LogWarning("Category with ID {CategoryId} not found for deletion", id);
                    return false;
                }

                _logger.LogInformation("Attempting to delete category '{CategoryName}' (ID: {CategoryId}). Related movies count: {MovieCount}",
                    category.Name, id, category.Movies?.Count ?? 0);

                // Remove the category-movie relationships first
                // This will only remove the many-to-many relationship, not the movies themselves
                if (category.Movies != null && category.Movies.Any())
                {
                    foreach (var movie in category.Movies.ToList())
                    {
                        movie.Categories.Remove(category);
                    }
                }

                // Remove the category itself
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully deleted category with ID {CategoryId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting category with ID {CategoryId}", id);
                return false;
            }
        }

        public async Task<bool> CategoryExistsAsync(int id)
        {
            try
            {
                return await _context.Categories.AnyAsync(c => c.Id == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking if category exists with ID {CategoryId}", id);
                throw;
            }
        }

        public async Task<int> GetMovieCountByCategoryAsync(int categoryId)
        {
            try
            {
                var category = await _context.Categories
                    .AsNoTracking()
                    .Include(c => c.Movies)
                    .FirstOrDefaultAsync(c => c.Id == categoryId);

                return category?.Movies?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting movie count for category ID {CategoryId}", categoryId);
                throw;
            }
        }
    }
}