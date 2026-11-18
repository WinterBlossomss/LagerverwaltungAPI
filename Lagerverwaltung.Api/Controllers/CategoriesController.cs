using Lagerverwaltung.Api.DTOs;
using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    [Route("api/categories")]
    public class CategoriesController : ApiControllerBase
    {
        public CategoriesController(LagerverwaltungssystemContext context) : base(context) { }

        /// <summary>GET /api/categories  (logged in)</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await Db.spGetAllCategoriesAsync(TokenHash));

        /// <summary>POST /api/categories  (Administrator)</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CategoryRequest request)
        {
            var result = await Db.spCreateCategoryAsync(TokenHash, request.Name.Trim(), request.ModelNum);
            int id = result.Single().NewId!.Value;
            return Created($"/api/categories/{id}", new CreatedResponse(id));
        }

        /// <summary>PUT /api/categories/{id}  (Administrator)</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request)
        {
            await Db.spAlterCategoryAsync(TokenHash, id, request.Name.Trim(), request.ModelNum);
            return NoContent();
        }

        /// <summary>DELETE /api/categories/{id}  (Administrator). 409 while articles use it.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await Db.spDeleteCategoryAsync(TokenHash, id);
            return NoContent();
        }
    }
}
