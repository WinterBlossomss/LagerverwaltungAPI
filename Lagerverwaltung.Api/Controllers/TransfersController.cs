using Lagerverwaltung.Api.DTOs;
using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    /// <summary>Ausgabe Hauptlager -> Labor and digital confirmation (Übernahme).</summary>
    [Route("api/transfers")]
    public class TransfersController : ApiControllerBase
    {
        public TransfersController(LagerverwaltungssystemContext context) : base(context) { }

        /// <summary>
        /// POST /api/transfers  (Administrator)
        /// Stock leaves the Hauptlager immediately (reserved) and the transfer is "offen".
        /// 409 if the Hauptlager doesn't have enough.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MovementRequest request)
        {
            var result = await Db.spCreateTransferAsync(TokenHash, request.LaborId, request.ArtikelId, request.Menge);
            int id = result.Single().NewId!.Value;
            return Created($"/api/transfers/{id}", new CreatedResponse(id));
        }

        /// <summary>GET /api/transfers/pending  (Administrator: all; LehrerIn: only for her labs)</summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending() => Ok(await Db.spGetPendingTransfersAsync(TokenHash));

        /// <summary>
        /// POST /api/transfers/{id}/confirm  (only the responsible LehrerIn)
        /// Credits the Labor and closes the transfer. A second confirm gives 409.
        /// </summary>
        [HttpPost("{id:int}/confirm")]
        public async Task<IActionResult> Confirm(int id)
        {
            await Db.spConfirmTransferAsync(TokenHash, id);
            return NoContent();
        }
    }
}
