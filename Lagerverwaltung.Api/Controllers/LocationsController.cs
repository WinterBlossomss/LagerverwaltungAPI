using Lagerverwaltung.Api.DTOs;
using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    [Route("api/locations")]
    public class LocationsController : ApiControllerBase
    {
        public LocationsController(LagerverwaltungssystemContext context) : base(context)
        {
        }

        /// <summary>
        /// Retrieves all rooms accessible to the current token.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await Db.spGetAllRoomsAsync(TokenHash));

        /// <summary>
        /// Retrieves the rooms owned by the authenticated user.
        /// </summary>
        [HttpGet("mine")]
        public async Task<IActionResult> GetMine() => Ok(new { items = await Db.spGetOwnRoomsAsync(TokenHash) });

        /// <summary>
        /// Creates a new room location.
        /// </summary>
        /// <param name="request">The location creation details.</param>
        /// <returns>A <see cref="IActionResult"/> containing the created location resource with HTTP status 201.</returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLocationRequest request)
        {
            var result = await Db.spCreateRoomAsync(TokenHash, request.RaumTyp, request.BenutzerId, request.RaumNum);
            int id = result.Single().NewId!.Value;
            return Created($"/api/locations/{id}", new CreatedResponse(id));
        }

        /// <summary>
        /// Updates an existing location.
        /// </summary>
        /// <param name="id">The identifier of the location to update.</param>
        /// <param name="request">The updated location data.</param>
        /// <returns>A no-content response when the update succeeds.</returns>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateLocationRequest request)
        {
            await Db.spAlterRoomAsync(TokenHash, id, request.RaumNum);
            return NoContent();
        }

        /// <summary>
        /// Assigns a teacher to the specified lab. (Wasn't in the assignment, but it's a useful endpoint.)
        /// </summary>
        /// <param name="id">The identifier of the lab.</param>
        /// <param name="request">The request containing the teacher identifier.</param>
        /// <returns>A task whose result is a no-content response.</returns>
        [HttpPut("{id:int}/teacher")]
        public async Task<IActionResult> AssignTeacher(int id, [FromBody] AssignTeacherRequest request)
        {
            await Db.spAssignTeacherToLabAsync(TokenHash, id, request.BenutzerId);
            return NoContent();
        }

        /// <summary>
        /// Deletes the room identified by the specified ID.
        /// </summary>
        /// <param name="id">The room identifier.</param>
        /// <returns>A no-content result after the room is deleted.</returns>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await Db.spDeleteRoomAsync(TokenHash, id);
            return NoContent();
        }

        /// <summary>
        /// Retrieves the inventory for a location.
        /// </summary>
        /// <param name="id">The location identifier.</param>
        /// <returns>An HTTP 200 response containing the location inventory.</returns>
        [HttpGet("{id:int}/inventory")]
        public async Task<IActionResult> GetInventory(int id) => Ok(await Db.spGetLocationInventoryAsync(TokenHash, id));
    }
}