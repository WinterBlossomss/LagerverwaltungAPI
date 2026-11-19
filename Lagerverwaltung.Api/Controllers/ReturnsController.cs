using Lagerverwaltung.Api.DTOs;
using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    /// <summary>Rückgabe Labor -> Umbuchungslager -> Hauptlager.</summary>
    [Route("api/returns")]
    public class ReturnsController : ApiControllerBase
    {
        public ReturnsController(LagerverwaltungssystemContext context) : base(context) { }

        /// <summary>
        /// POST /api/returns  (only the responsible LehrerIn)
        /// Books Labor -> Umbuchungslager. The target is fixed by the procedure,
        /// a LehrerIn can never book directly into the Hauptlager.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MovementRequest request)
        {
            return await Run(async () =>
            {
                var result = await Db.spCreateReturnAsync(TokenHash, request.LaborId, request.ArtikelId, request.Menge);
                int id = result.Single().NewId!.Value;
                return Created($"/api/returns/{id}", new CreatedResponse(id));
            });
        }

        /// <summary>GET /api/returns/pending  (Administrator)</summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending() => await Run(async () => Ok(await Db.spGetPendingReturnsAsync(TokenHash)));

        /// <summary>POST /api/returns/{id}/accept  (Administrator): Umbuchungslager -> Hauptlager, return closed.</summary>
        [HttpPost("{id:int}/accept")]
        public async Task<IActionResult> Accept(int id)
        {
            return await Run(async () =>
            {
                await Db.spAcceptReturnAsync(TokenHash, id);
                return NoContent();
            });
        }
    }
}
