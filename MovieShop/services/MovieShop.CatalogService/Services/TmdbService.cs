using System.Text.Json;
using MovieShop.CatalogService.DTOs;

namespace MovieShop.CatalogService.Services;

public interface ITmdbService
{
    Task<TmdbMovieDto?> SearchMovieAsync(string title);

    /// <summary>
    /// Az első YouTube-os előzetes, vagy null, ha nincs. HTTP-hiba esetén kivételt
    /// dob — a monolit MovieController-e ezt 500-as „Error fetching trailer" válaszra fordította.
    /// </summary>
    Task<TmdbVideoDto?> GetTrailerAsync(int tmdbId);

    /// <summary>
    /// Bővített TMDB-adatok a filmadatlaphoz: háttérképek, poszterek, szereplők, rendező,
    /// műfajok, játékidő. Null, ha a TMDB nem érhető el vagy a film nem található.
    /// </summary>
    Task<TmdbMovieExtrasDto?> GetMovieExtrasAsync(int tmdbId);
}

public class TmdbService : ITmdbService
{
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<TmdbService> _logger;

    public TmdbService(HttpClient httpClient, IConfiguration configuration, ILogger<TmdbService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["TmdbApi:ApiKey"] ?? string.Empty;
    }

    /// <summary>
    /// Eltérés a monolittól: ha a TMDB nem érhető el, itt null jön vissza, és a
    /// filmadatlap TMDB-adatok nélkül töltődik be. A monolitban ilyenkor a teljes
    /// filmadatlap-kérés 500-as hibával zárult.
    /// </summary>
    public async Task<TmdbMovieDto?> SearchMovieAsync(string title)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        try
        {
            var response = await _httpClient.GetAsync(
                $"{BaseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(title)}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<TmdbSearchResultDto>(content, JsonOptions);

            return searchResult?.Results.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TMDB search failed: {Title}", title);
            return null;
        }
    }

    public async Task<TmdbVideoDto?> GetTrailerAsync(int tmdbId)
    {
        // A monolit HttpClient.GetStringAsync-et használt, ami nem-sikeres válaszra kivételt dob
        var response = await _httpClient.GetStringAsync($"{BaseUrl}/movie/{tmdbId}/videos?api_key={_apiKey}");
        var videoResponse = JsonSerializer.Deserialize<TmdbVideoResponseDto>(response, JsonOptions);

        return videoResponse?.Results?.FirstOrDefault(v =>
            v.Type.Equals("Trailer", StringComparison.OrdinalIgnoreCase) &&
            v.Site.Equals("YouTube", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TmdbMovieExtrasDto?> GetMovieExtrasAsync(int tmdbId)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        try
        {
            // Egyetlen kérés: részletek + képek + stáb; a képeknél az angol és a szöveg nélküli változatok kellenek
            var response = await _httpClient.GetAsync(
                $"{BaseUrl}/movie/{tmdbId}?api_key={_apiKey}&append_to_response=images,credits&include_image_language=en,null");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var movie = JsonSerializer.Deserialize<TmdbMovieFullDto>(content, JsonOptions);

            return movie == null ? null : TmdbMovieExtrasDto.From(movie);
        }
        catch (Exception ex)
        {
            // A bővített adatok opcionálisak: TMDB-hiba esetén a filmadatlap nélkülük is betölt
            _logger.LogWarning(ex, "TMDB extras request failed: {TmdbId}", tmdbId);
            return null;
        }
    }
}
