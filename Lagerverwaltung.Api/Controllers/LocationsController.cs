using Lagerverwaltung.Api.DTOs;
using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    /// <summary>Locations = Hauptlager, Umbuchungslager and Labore (table Raum).</summary>
    [Route("api/locations")]
    public class LocationsController : ApiControllerBase
    {
        public LocationsController(LagerverwaltungssystemContext context) : base(context) { }

        /// <summary>GET /api/locations  (Administrator)</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll() => await Run(async () => Ok(await Db.spGetAllRoomsAsync(TokenHash)));

        /// <summary>GET /api/locations/mine  (logged in): the rooms I am responsible for.</summary>
        [HttpGet("mine")]
        public async Task<IActionResult> GetMine() => await Run(async () => Ok(await Db.spGetOwnRoomsAsync(TokenHash)));

        /// <summary>POST /api/locations  (Administrator)</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLocationRequest request)
        {
            return await Run(async () =>
            {
                var result = await Db.spCreateRoomAsync(TokenHash, request.RaumTyp, request.BenutzerId, request.RaumNum);
                int id = result.Single().NewId!.Value;
                return Created($"/api/locations/{id}", new CreatedResponse(id));
            });
        }

        /// <summary>PUT /api/locations/{id}  (Administrator): change the room number.</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateLocationRequest request)
        {
            return await Run(async () =>
            {
                await Db.spAlterRoomAsync(TokenHash, id, request.RaumNum);
                return NoContent();
            });
        }

        /// <summary>PUT /api/locations/{id}/teacher  (Administrator): assign the responsible LehrerIn to a Labor.</summary>
        [HttpPut("{id:int}/teacher")]
        public async Task<IActionResult> AssignTeacher(int id, [FromBody] AssignTeacherRequest request)
        {
            return await Run(async () =>
            {
                await Db.spAssignTeacherToLabAsync(TokenHash, id, request.BenutzerId);
                return NoContent();
            });
        }

        /// <summary>DELETE /api/locations/{id}  (Administrator). 409 if it has stock or history.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await Run(async () =>
            {
                await Db.spDeleteRoomAsync(TokenHash, id);
                return NoContent();
            });
        }

        /// <summary>GET /api/locations/{id}/inventory  (Administrator or the responsible LehrerIn, otherwise 403)</summary>
        [HttpGet("{id:int}/inventory")]
        public async Task<IActionResult> GetInventory(int id) => await Run(async () => Ok(await Db.spGetLocationInventoryAsync(TokenHash, id)));
    }
}
