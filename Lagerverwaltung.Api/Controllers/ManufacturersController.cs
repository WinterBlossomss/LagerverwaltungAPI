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

        /// <summary>GET /api/manufacturers  (logged in)</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll() => await Run(async () => Ok(await Db.spGetAllManufacturersAsync(TokenHash)));

        /// <summary>POST /api/manufacturers  (Administrator)</summary>
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

        /// <summary>PUT /api/manufacturers/{id}  (Administrator)</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ManufacturerRequest request)
        {
            return await Run(async () =>
            {
                await Db.spAlterManufacturerAsync(TokenHash, id, request.Name.Trim());
                return NoContent();
            });
        }

        /// <summary>DELETE /api/manufacturers/{id}  (Administrator). 409 while articles use it.</summary>
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
