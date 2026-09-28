using System.Text.Json.Serialization;

namespace MovieShop.CatalogService.DTOs;

public class MovieListDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Price { get; set; }
    public int? DiscountedPrice { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class MovieAdminListDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int Price { get; set; }
    public int? DiscountedPrice { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string Status => IsDeleted ? "Deleted" : "Active";
}

public class MovieDetailsDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; }
    public int? DiscountedPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public List<CategoryDto>? Categories { get; set; }
    public List<ReviewDto>? Reviews { get; set; }
    public string? VideoFileName { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class MovieDetailsWithTmdbDto : MovieDetailsDto
{
    public TmdbMovieInfo? TmdbInfo { get; set; }
}

public class TmdbMovieInfo
{
    public int TmdbId { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public string? ReleaseDate { get; set; }
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A monolit ReviewDto-jával azonos alak: az AdminReviews oldal a beágyazott
/// `movie.title` és `user.name` mezőket, a ReviewList a `userName` mezőt olvassa.
/// A felhasználói adatok a lokális UserSnapshot projekcióból jönnek.
/// </summary>
public class ReviewDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MovieListDto? Movie { get; set; }
    public UserDto? User { get; set; }

    public int UserId { get; set; }
    public int MovieId { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class ReviewCreateDto
{
    public string Content { get; set; } = string.Empty;
}

public class MovieTrailerDto
{
    public string? YoutubeKey { get; set; }
    public string? Url { get; set; }
    public string? Name { get; set; }
    public string? Message { get; set; }
}

public class VideoProgressDto
{
    public double ProgressSeconds { get; set; }
    public DateTime LastWatched { get; set; }
}

public class SaveProgressDto
{
    public double ProgressSeconds { get; set; }
}

// ── Elasticsearch keresés ────────────────────────────────────────────────────

public class SearchRequestDto
{
    public string? Q { get; set; }
    public string[]? Categories { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public double? MinRating { get; set; }

    /// <summary>price-asc | price-desc | name-asc | name-desc | rating-desc</summary>
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;
    public int Size { get; set; } = 8;
}

public class SearchResultDto
{
    public IEnumerable<MovieSearchItemDto> Movies { get; set; } = [];
    public long Total { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
}

public class MovieSearchItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string[] Categories { get; set; } = [];
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

// ── TMDB ─────────────────────────────────────────────────────────────────────

public class TmdbMovieDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Overview { get; set; }
    // A TMDB snake_case mezőneveket küld — attribútum nélkül ezek 0/null értéken maradtak
    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }
    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }
    [JsonPropertyName("vote_count")]
    public int VoteCount { get; set; }
    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }
}

public class TmdbSearchResultDto
{
    public List<TmdbMovieDto> Results { get; set; } = [];
}

public class TmdbVideoDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class TmdbVideoResponseDto
{
    public List<TmdbVideoDto>? Results { get; set; }
}
