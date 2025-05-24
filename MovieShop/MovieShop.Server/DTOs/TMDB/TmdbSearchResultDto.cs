namespace MovieShop.Server.DTOs.TMDB
{
    public class TmdbSearchResultDto
    {
        public List<TmdbMovieDto> Results { get; set; } = new List<TmdbMovieDto>();
        public int Page { get; set; }
        public int TotalResults { get; set; }
        public int TotalPages { get; set; }
    }
}
