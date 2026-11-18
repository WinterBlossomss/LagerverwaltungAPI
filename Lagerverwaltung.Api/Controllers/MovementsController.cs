using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    [Route("api/movements")]
    public class MovementsController : ApiControllerBase
    {
        public MovementsController(LagerverwaltungssystemContext context) : base(context) { }

        /// <summary>
        /// GET /api/movements?raumId=&amp;artikelId=
        /// Administrator: full history. LehrerIn: only movements of her own rooms.
        /// There is no endpoint to delete movements: the history stays complete.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? raumId, [FromQuery] int? artikelId)
            => Ok(await Db.spGetAllMovementsAsync(TokenHash, raumId, artikelId));
    }
}
