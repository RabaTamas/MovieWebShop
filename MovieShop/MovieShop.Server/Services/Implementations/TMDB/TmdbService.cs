using MovieShop.Server.DTOs.TMDB;
using MovieShop.Server.Services.Interfaces.TMDB;
using System.Text.Json;

namespace MovieShop.Server.Services.Implementations.TMDB
{
    public class TmdbService : ITmdbService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://api.themoviedb.org/3";

        public TmdbService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["TmdbApi:ApiKey"] ?? throw new InvalidOperationException("TMDB API key not found in configuration");
        }

        public async Task<TmdbMovieDto?> SearchMovieAsync(string title)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(title)}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<TmdbSearchResultDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Return the first result if any found
            return searchResult?.Results.FirstOrDefault();
        }

        public async Task<TmdbMovieDetailsDto?> GetMovieDetailsAsync(int tmdbId)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/movie/{tmdbId}?api_key={_apiKey}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TmdbMovieDetailsDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
