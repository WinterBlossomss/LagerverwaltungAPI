using Lagerverwaltung.Api.DTOs;
using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    [Route("api/articles")]
    public class ArticlesController : ApiControllerBase
    {
        public ArticlesController(LagerverwaltungssystemContext context) : base(context) { }

        /// <summary>
        /// GET /api/articles?name=&amp;serienNum=&amp;kategorieId=&amp;herstellerId=&amp;raumId=  (logged in)
        /// Five optional filters. "Bestand" respects permissions: a LehrerIn only sees stock in her own rooms,
        /// and filtering by a foreign room gives 403.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] ArticleFilter filter)
        {
            var articles = await Db.spGetAllArtikelFilteredAsync(
                TokenHash, filter.Name, filter.SerienNum, filter.KategorieId, filter.HerstellerId, filter.RaumId);
            return Ok(articles);
        }

        /// <summary>GET /api/articles/{id}  (logged in)</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var article = (await Db.spGetArtikelByIdAsync(TokenHash, id)).Single();
            return Ok(article);
        }

        /// <summary>POST /api/articles  (Administrator). Creates the article with stock 0.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ArticleRequest request)
        {
            var result = await Db.spCreateArtikelAsync(
                TokenHash, request.Name.Trim(), request.SerienNum, request.KategorieId, request.HerstellerId);
            int id = result.Single().NewId!.Value;
            return Created($"/api/articles/{id}", new CreatedResponse(id));
        }

        /// <summary>PUT /api/articles/{id}  (Administrator)</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ArticleRequest request)
        {
            await Db.spAlterArtikelAsync(
                TokenHash, id, request.Name.Trim(), request.SerienNum, request.KategorieId, request.HerstellerId);
            return NoContent();
        }

        /// <summary>DELETE /api/articles/{id}  (Administrator). 409 if it has stock or history.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await Db.spDeleteArtikelAsync(TokenHash, id);
            return NoContent();
        }

        /// <summary>
        /// POST /api/articles/{id}/stock  (Administrator) - "Zugang": new pieces arrive in the Hauptlager.
        /// For an article with serial number only Menge = 1 is allowed, and only once.
        /// </summary>
        [HttpPost("{id:int}/stock")]
        public async Task<IActionResult> AddStock(int id, [FromBody] AddStockRequest request)
        {
            var result = await Db.spAddStockAsync(TokenHash, id, request.Menge);
            return Ok(new { movementId = result.Single().NewId });
        }
    }
}
