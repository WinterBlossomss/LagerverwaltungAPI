using System.ComponentModel.DataAnnotations;

namespace Lagerverwaltung.Api.DTOs
{
    public record CategoryRequest(
        [Required, StringLength(100, MinimumLength = 1)] string Name,
        int? ModelNum);

    /// <summary>SerienNum = null: article is counted by quantity. With SerienNum: exactly one device.</summary>
    public record ArticleRequest(
        [Required, StringLength(100, MinimumLength = 1)] string Name,
        [Range(1, int.MaxValue)] int? SerienNum,
        [Required] int? KategorieId,
        int? HerstellerId);

    public record AddStockRequest([Required, Range(1, 100000)] int? Menge);

    /// <summary>Query parameters for GET /api/articles (all optional).</summary>
    public record ArticleFilter(string? Name, int? SerienNum, int? KategorieId, int? HerstellerId, int? RaumId);
}
