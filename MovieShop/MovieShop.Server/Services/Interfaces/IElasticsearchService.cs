using MovieShop.Server.DTOs;
using MovieShop.Server.Models;

namespace MovieShop.Server.Services.Interfaces
{
    public interface IElasticsearchService
    {
        Task IndexMovieAsync(Movie movie);
        Task DeleteMovieFromIndexAsync(int movieId);
        Task<SearchResultDto> SearchAsync(SearchRequestDto request);
        Task<IEnumerable<string>> AutocompleteAsync(string prefix);
        Task ReindexAllAsync();
    }
}
