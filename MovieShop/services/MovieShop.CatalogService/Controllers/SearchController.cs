using Microsoft.AspNetCore.Mvc;
using MovieShop.CatalogService.DTOs;
using MovieShop.CatalogService.Services;

namespace MovieShop.CatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IElasticsearchService _es;

    public SearchController(IElasticsearchService es) => _es = es;

    // GET /api/Search?q=batman&categories=Action&minPrice=5&maxPrice=20&page=1&size=8
    [HttpGet]
    public async Task<ActionResult<SearchResultDto>> Search([FromQuery] SearchRequestDto request)
        => Ok(await _es.SearchAsync(request));

    // GET /api/Search/autocomplete?q=bat
    [HttpGet("autocomplete")]
    public async Task<ActionResult<IEnumerable<string>>> Autocomplete([FromQuery] string q)
        => Ok(await _es.AutocompleteAsync(q));

    // POST /api/Search/reindex — a monolitban sem volt jogosultsághoz kötve
    [HttpPost("reindex")]
    public async Task<ActionResult> Reindex()
    {
        await _es.ReindexAllAsync();
        return Ok(new { message = "Reindex complete" });
    }
}
