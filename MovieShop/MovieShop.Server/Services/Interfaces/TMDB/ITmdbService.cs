using MovieShop.Server.DTOs.TMDB;

namespace MovieShop.Server.Services.Interfaces.TMDB
{
    public interface ITmdbService
    {
        Task<TmdbMovieDto?> SearchMovieAsync(string title);
        Task<TmdbMovieDetailsDto?> GetMovieDetailsAsync(int tmdbId);
    }
}
