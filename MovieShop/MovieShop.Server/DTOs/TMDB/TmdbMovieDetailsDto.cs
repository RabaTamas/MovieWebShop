using System.Text.Json.Serialization;

namespace MovieShop.Server.DTOs.TMDB
{
    public class TmdbMovieDetailsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }
        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }
        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }
        public string Overview { get; set; } = string.Empty;
        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }
    }
}
