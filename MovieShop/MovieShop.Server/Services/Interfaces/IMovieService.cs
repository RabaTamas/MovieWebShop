using MovieShop.Server.DTOs;
using MovieShop.Server.DTOs.TMDB;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IMovieService
    {
        Task<IEnumerable<MovieListDto>> GetAllMoviesAsync();
        Task<MovieDetailsWithTmdbDto?> GetMovieByIdAsync(int id);
        Task<IEnumerable<MovieListDto>> GetMoviesByCategoryAsync(int categoryId);

        Task<IEnumerable<MovieListDto>> GetMoviesByCategoriesAsync(IEnumerable<int> categoryIds);
        Task<bool> AddMovieAsync(MovieDetailsDto movieDto);
        Task<bool> UpdateMovieAsync(int id, MovieDetailsDto movieDto);
        //Task<bool> DeleteMovieAsync(int id);
        Task<bool> AddCategoryToMovieAsync(int movieId, int categoryId);
        Task<bool> RemoveCategoryFromMovieAsync(int movieId, int categoryId);


        Task<IEnumerable<MovieAdminListDto>> GetAllMoviesForAdminAsync();
        Task<IEnumerable<MovieAdminListDto>> GetDeletedMoviesAsync();
        Task<MovieDetailsDto?> GetMovieByIdForAdminAsync(int id);

        Task<bool> DeleteMovieAsync(int id); // Soft delete
        Task<bool> RestoreMovieAsync(int id); // Restore soft-deleted movie
        
        Task UpdateVideoFileNameAsync(int movieId, string? videoFileName);
    }
}
