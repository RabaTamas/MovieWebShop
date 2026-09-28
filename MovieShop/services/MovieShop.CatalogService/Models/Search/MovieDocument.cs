using Nest;

namespace MovieShop.CatalogService.Models.Search;

[ElasticsearchType(IdProperty = nameof(Id))]
public class MovieDocument
{
    public int Id { get; set; }

    [Text(Analyzer = "standard", Boost = 3)]
    public string Title { get; set; } = string.Empty;

    [Text(Analyzer = "standard")]
    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public string ImageUrl { get; set; } = string.Empty;

    [Keyword]
    public string[] Categories { get; set; } = [];

    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
