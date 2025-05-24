using MovieShop.Server.DTOs;

namespace MovieShop.Server.Services.Interfaces
{
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
}
