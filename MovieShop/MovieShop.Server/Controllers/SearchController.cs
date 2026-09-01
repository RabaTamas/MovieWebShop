using Microsoft.AspNetCore.Mvc;
using MovieShop.Server.DTOs;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly IElasticsearchService _es;

        public SearchController(IElasticsearchService es) => _es = es;

        // GET /api/Search?q=batman&categories=Action&minPrice=5&maxPrice=20&page=1&size=8
        [HttpGet]
        public async Task<ActionResult<SearchResultDto>> Search([FromQuery] SearchRequestDto request)
        {
            var result = await _es.SearchAsync(request);
            return Ok(result);
        }

        // GET /api/Search/autocomplete?q=bat
        [HttpGet("autocomplete")]
        public async Task<ActionResult<IEnumerable<string>>> Autocomplete([FromQuery] string q)
        {
            var suggestions = await _es.AutocompleteAsync(q);
            return Ok(suggestions);
        }

        // POST /api/Search/reindex  (admin only, useful after data migrations)
        [HttpPost("reindex")]
        public async Task<ActionResult> Reindex()
        {
            await _es.ReindexAllAsync();
            return Ok(new { message = "Reindex complete" });
        }
    }
}
