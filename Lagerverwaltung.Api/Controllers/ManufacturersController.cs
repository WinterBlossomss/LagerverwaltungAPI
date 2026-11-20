using Lagerverwaltung.Api.DTOs;
using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    /// <summary>Manufacturers (table Hersteller). Same pattern as CategoriesController.</summary>
    [Route("api/manufacturers")]
    public class ManufacturersController : ApiControllerBase
    {
        public ManufacturersController(LagerverwaltungssystemContext context) : base(context) { }

        /// <summary>
        /// Gets all manufacturers.
        /// </summary>
        /// <returns>An <c>IActionResult</c> containing the manufacturers.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll() => await Run(async () => Ok(await Db.spGetAllManufacturersAsync(TokenHash)));

        /// <summary>
        /// Creates a manufacturer.
        /// </summary>
        /// <param name="request">The manufacturer details.</param>
        /// <returns>A created response containing the new manufacturer identifier.</returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ManufacturerRequest request)
        {
            return await Run(async () =>
            {
                var result = await Db.spCreateManufacturerAsync(TokenHash, request.Name.Trim());
                int id = result.Single().NewId!.Value;
                return Created($"/api/manufacturers/{id}", new CreatedResponse(id));
            });
        }

        /// <summary>
        /// Updates the manufacturer with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier of the manufacturer to update.</param>
        /// <param name="request">The manufacturer details to apply.</param>
        /// <returns>A task that represents the asynchronous operation and contains an <see cref="IActionResult"/>.</returns>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ManufacturerRequest request)
        {
            return await Run(async () =>
            {
                await Db.spAlterManufacturerAsync(TokenHash, id, request.Name.Trim());
                return NoContent();
            });
        }

        /// <summary>
        /// Deletes the manufacturer with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier of the manufacturer to delete.</param>
        /// <returns>An <see cref="IActionResult"/> that contains a no-content response when the manufacturer is deleted.</returns>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await Run(async () =>
            {
                await Db.spDeleteManufacturerAsync(TokenHash, id);
                return NoContent();
            });
        }
    }
}
