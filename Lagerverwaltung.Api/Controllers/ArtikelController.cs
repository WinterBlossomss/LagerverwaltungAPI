using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    [Route("api/articles")]
    [ApiController]
    public class ArtikelController : ControllerBase
    {
        private readonly LagerverwaltungssystemContextProcedures _context;

        public ArtikelController(LagerverwaltungssystemContextProcedures context)
        {
            _context = context;
        }

        // GET: api/articles
        [HttpGet]
        public async Task<IActionResult> GetAllArticles(byte[] tokenHash, string? searchQuery, int? categoryId, int? manifacturerId, int? serialnum)
        {
            try
            {
                var articles = await _context.spGetAllArtikelFilteredAsync(tokenHash, searchQuery, serialnum, categoryId, manifacturerId);
                return Ok(articles);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }
    }
}