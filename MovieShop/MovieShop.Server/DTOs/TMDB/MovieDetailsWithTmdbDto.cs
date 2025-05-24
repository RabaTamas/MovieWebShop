namespace MovieShop.Server.DTOs.TMDB
{
    public class MovieDetailsWithTmdbDto : MovieDetailsDto
    {
        public TmdbMovieInfo? TmdbInfo { get; set; }
    }
}
