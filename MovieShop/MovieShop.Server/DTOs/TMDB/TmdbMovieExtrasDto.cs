using System.Text.Json.Serialization;

namespace MovieShop.Server.DTOs.TMDB
{
    // ── TMDB nyers válasz: /movie/{id}?append_to_response=images,credits ─────────

    public class TmdbMovieFullDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        [JsonPropertyName("original_title")]
        public string? OriginalTitle { get; set; }
        public string? Tagline { get; set; }
        public string? Overview { get; set; }
        public int? Runtime { get; set; }
        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }
        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }
        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }
        public List<TmdbGenreDto> Genres { get; set; } = new();
        [JsonPropertyName("original_language")]
        public string? OriginalLanguage { get; set; }
        public long Budget { get; set; }
        public long Revenue { get; set; }
        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }
        [JsonPropertyName("backdrop_path")]
        public string? BackdropPath { get; set; }
        public TmdbImagesDto? Images { get; set; }
        public TmdbCreditsDto? Credits { get; set; }
    }

    public class TmdbGenreDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TmdbImagesDto
    {
        public List<TmdbImageFileDto> Backdrops { get; set; } = new();
        public List<TmdbImageFileDto> Posters { get; set; } = new();
    }

    public class TmdbImageFileDto
    {
        [JsonPropertyName("file_path")]
        public string FilePath { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        [JsonPropertyName("aspect_ratio")]
        public double AspectRatio { get; set; }
        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }
        [JsonPropertyName("iso_639_1")]
        public string? Language { get; set; }
    }

    public class TmdbCreditsDto
    {
        public List<TmdbCastDto> Cast { get; set; } = new();
        public List<TmdbCrewDto> Crew { get; set; } = new();
    }

    public class TmdbCastDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Character { get; set; }
        [JsonPropertyName("profile_path")]
        public string? ProfilePath { get; set; }
        public int Order { get; set; }
    }

    public class TmdbCrewDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Job { get; set; }
    }

    // ── A frontendnek küldött, kész URL-ekkel feltöltött bővített adatok ─────────

    public class TmdbMovieExtrasDto
    {
        private const string ImageBaseUrl = "https://image.tmdb.org/t/p/";

        public int TmdbId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? OriginalTitle { get; set; }
        public string? Tagline { get; set; }
        public string? Overview { get; set; }
        public int? Runtime { get; set; }
        public string? ReleaseDate { get; set; }
        public double VoteAverage { get; set; }
        public int VoteCount { get; set; }
        public List<string> Genres { get; set; } = new();
        public string? OriginalLanguage { get; set; }
        public long Budget { get; set; }
        public long Revenue { get; set; }
        public string? PosterUrl { get; set; }
        public string? BackdropUrl { get; set; }
        public List<TmdbImageDto> Backdrops { get; set; } = new();
        public List<TmdbImageDto> Posters { get; set; } = new();
        public List<TmdbCastMemberDto> Cast { get; set; } = new();
        public List<string> Directors { get; set; } = new();

        public static TmdbMovieExtrasDto From(TmdbMovieFullDto movie)
        {
            static string? Image(string? path, string size) =>
                string.IsNullOrEmpty(path) ? null : $"{ImageBaseUrl}{size}{path}";

            return new TmdbMovieExtrasDto
            {
                TmdbId = movie.Id,
                Title = movie.Title,
                OriginalTitle = movie.OriginalTitle,
                Tagline = string.IsNullOrWhiteSpace(movie.Tagline) ? null : movie.Tagline,
                Overview = movie.Overview,
                Runtime = movie.Runtime,
                ReleaseDate = string.IsNullOrEmpty(movie.ReleaseDate) ? null : movie.ReleaseDate,
                VoteAverage = movie.VoteAverage,
                VoteCount = movie.VoteCount,
                Genres = movie.Genres.Select(g => g.Name).ToList(),
                OriginalLanguage = movie.OriginalLanguage,
                Budget = movie.Budget,
                Revenue = movie.Revenue,
                PosterUrl = Image(movie.PosterPath, "w500"),
                BackdropUrl = Image(movie.BackdropPath, "original"),
                // Szöveg nélküli (nyelvfüggetlen) háttérképek előre, azon belül a legjobbra értékeltek
                Backdrops = (movie.Images?.Backdrops ?? new())
                    .OrderBy(b => b.Language == null ? 0 : 1)
                    .ThenByDescending(b => b.VoteAverage)
                    .Take(16)
                    .Select(b => new TmdbImageDto
                    {
                        Url = Image(b.FilePath, "w1280")!,
                        ThumbnailUrl = Image(b.FilePath, "w780")!,
                        Width = b.Width,
                        Height = b.Height,
                        AspectRatio = b.AspectRatio
                    })
                    .ToList(),
                Posters = (movie.Images?.Posters ?? new())
                    .OrderByDescending(p => p.VoteAverage)
                    .Take(8)
                    .Select(p => new TmdbImageDto
                    {
                        Url = Image(p.FilePath, "w780")!,
                        ThumbnailUrl = Image(p.FilePath, "w342")!,
                        Width = p.Width,
                        Height = p.Height,
                        AspectRatio = p.AspectRatio
                    })
                    .ToList(),
                Cast = (movie.Credits?.Cast ?? new())
                    .OrderBy(c => c.Order)
                    .Take(12)
                    .Select(c => new TmdbCastMemberDto
                    {
                        Name = c.Name,
                        Character = c.Character,
                        ProfileUrl = Image(c.ProfilePath, "w185")
                    })
                    .ToList(),
                Directors = (movie.Credits?.Crew ?? new())
                    .Where(c => c.Job == "Director")
                    .Select(c => c.Name)
                    .Distinct()
                    .ToList()
            };
        }
    }

    public class TmdbImageDto
    {
        public string Url { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public double AspectRatio { get; set; }
    }

    public class TmdbCastMemberDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Character { get; set; }
        public string? ProfileUrl { get; set; }
    }
}
