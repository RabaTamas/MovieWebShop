using MovieShop.Server.DTOs.TMDB;

namespace MovieShop.Server.Services.Interfaces.TMDB
{
    public interface ITmdbService
    {
        Task<TmdbMovieDto?> SearchMovieAsync(string title);
        Task<TmdbMovieDetailsDto?> GetMovieDetailsAsync(int tmdbId);

        /// <summary>
        /// Bővített TMDB-adatok a filmadatlaphoz: háttérképek, poszterek, szereplők, rendező,
        /// műfajok, játékidő. Null, ha a TMDB nem érhető el vagy a film nem található.
        /// </summary>
        Task<TmdbMovieExtrasDto?> GetMovieExtrasAsync(int tmdbId);
    }
}
