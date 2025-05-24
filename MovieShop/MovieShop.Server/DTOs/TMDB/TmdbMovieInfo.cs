namespace MovieShop.Server.DTOs.TMDB
{
    public class TmdbMovieInfo
    {
        public int TmdbId { get; set; }
        public double VoteAverage { get; set; }
        public int VoteCount { get; set; }
        public string? ReleaseDate { get; set; }
    }
}
