using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(ICategoryService categoryService, ILogger<CategoryController> logger)
        {
            _categoryService = categoryService;
            _logger = logger;
        }

       
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAllCategories()
        {
            try
            {
                var categories = await _categoryService.GetAllCategoriesAsync();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching all categories");
                return StatusCode(500, new { message = "An error occurred while fetching categories" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CategoryDto>> GetCategory(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { message = "Invalid category ID" });
                }

                var category = await _categoryService.GetCategoryByIdAsync(id);
                if (category == null)
                {
                    return NotFound(new { message = $"Category with ID {id} not found" });
                }

                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching category with ID {CategoryId}", id);
                return StatusCode(500, new { message = "An error occurred while fetching the category" });
            }
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost]
        public async Task<ActionResult> AddCategory(CategoryDto categoryDto)
        {
            try
            {
                if (categoryDto == null)
                {
                    return BadRequest(new { message = "Category data is required" });
                }

                if (string.IsNullOrWhiteSpace(categoryDto.Name))
                {
                    return BadRequest(new { message = "Category name is required" });
                }

                if (categoryDto.Name.Length > 100) 
                {
                    return BadRequest(new { message = "Category name is too long" });
                }

                var result = await _categoryService.AddCategoryAsync(categoryDto);
                if (!result)
                {
                    return BadRequest(new { message = "Failed to add category. It may already exist." });
                }

                return CreatedAtAction(nameof(GetCategory), new { id = categoryDto.Id }, categoryDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while adding category '{CategoryName}'", categoryDto?.Name);
                return StatusCode(500, new { message = "An error occurred while adding the category" });
            }
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateCategory(int id, CategoryDto categoryDto)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { message = "Invalid category ID" });
                }

                if (categoryDto == null)
                {
                    return BadRequest(new { message = "Category data is required" });
                }

                if (id != categoryDto.Id)
                {
                    return BadRequest(new { message = "ID mismatch between URL and request body" });
                }

                if (string.IsNullOrWhiteSpace(categoryDto.Name))
                {
                    return BadRequest(new { message = "Category name is required" });
                }

                if (categoryDto.Name.Length > 100) // Assuming max length
                {
                    return BadRequest(new { message = "Category name is too long" });
                }

                var result = await _categoryService.UpdateCategoryAsync(id, categoryDto);
                if (!result)
                {
                    return NotFound(new { message = $"Category with ID {id} not found or name already exists" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating category with ID {CategoryId}", id);
                return StatusCode(500, new { message = "An error occurred while updating the category" });
            }
        }

        [Authorize(Policy = "RequireAdminRole")]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteCategory(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { message = "Invalid category ID" });
                }

                var movieCount = await _categoryService.GetMovieCountByCategoryAsync(id);
                _logger.LogInformation("Deleting category ID {CategoryId} which is associated with {MovieCount} movies", id, movieCount);

                var result = await _categoryService.DeleteCategoryAsync(id);
                if (!result)
                {
                    return NotFound(new { message = $"Category with ID {id} not found" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting category with ID {CategoryId}", id);
                return StatusCode(500, new { message = "An error occurred while deleting the category" });
            }
        }

        [HttpHead("{id}")]
        public async Task<ActionResult> CategoryExists(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest();
                }

                var exists = await _categoryService.CategoryExistsAsync(id);
                return exists ? Ok() : NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking if category exists with ID {CategoryId}", id);
                return StatusCode(500);
            }
        }

        [HttpGet("{id}/movie-count")]
        public async Task<ActionResult<int>> GetMovieCountByCategory(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest(new { message = "Invalid category ID" });
                }

                var count = await _categoryService.GetMovieCountByCategoryAsync(id);
                return Ok(new { categoryId = id, movieCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting movie count for category ID {CategoryId}", id);
                return StatusCode(500, new { message = "An error occurred while fetching movie count" });
            }
        }
    }
}