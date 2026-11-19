using Lagerverwaltung.Api.DTOs;
using Lagerverwaltung.Api.Models;
using Lagerverwaltung.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    [Route("api/users")]
    public class UsersController : ApiControllerBase
    {
        private readonly PasswordService _passwords;

        public UsersController(LagerverwaltungssystemContext context, PasswordService passwords) : base(context)
        {
            _passwords = passwords;
        }

        /// <summary>GET /api/users/me  (logged in)</summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            return await Run(async () =>
            {
                var me = (await Db.spGetOwnUserAsync(TokenHash)).Single();
                return Ok(me);
            });
        }

        /// <summary>
        /// PUT /api/users/me  (logged in)
        /// The current password is checked first. A new password gets a NEW salt (PasswordService.Hash).
        /// After a password change, the procedure revokes all other tokens of this user.
        /// </summary>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateOwnProfileRequest request)
        {
            return await Run(async () =>
            {
                if (request.NewUsername is null && request.NewPassword is null)
                    return BadRequest(new { error = "Keine Änderung angegeben." });

                var me = (await Db.spGetOwnUserAsync(TokenHash)).Single();          // 401 if the token is invalid
                var login = (await Db.spGetUserForLoginAsync(me.Username)).Single();
                if (!_passwords.Verify(request.CurrentPassword, login.PasswordHash))
                    return BadRequest(new { error = "Aktuelles Kennwort ist falsch." });

                string? newHash = request.NewPassword is null ? null : _passwords.Hash(request.NewPassword);
                await Db.spUpdateOwnProfileAsync(TokenHash, request.NewUsername?.Trim(), newHash);
                return NoContent();
            });
        }

        /// <summary>DELETE /api/users/me  (logged in). Refused with 409 if history would be lost.</summary>
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteMe()
        {
            return await Run(async () =>
            {
                await Db.spDeleteUserAsync(TokenHash, null);
                return NoContent();
            });
        }

        /// <summary>GET /api/users  (Administrator)</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll() => await Run(async () => Ok(await Db.spGetAllUsersAsync(TokenHash)));

        /// <summary>PUT /api/users/{id}/role  (Administrator). Body: { "role": "Administrator" | "LehrerIn" }</summary>
        [HttpPut("{id:int}/role")]
        public async Task<IActionResult> ChangeRole(int id, [FromBody] ChangeRoleRequest request)
        {
            return await Run(async () =>
            {
                await Db.spAlterUserRoleAsync(TokenHash, id, request.Role);
                return NoContent();
            });
        }

        /// <summary>DELETE /api/users/{id}  (Administrator). Refused with 409 if history would be lost.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await Run(async () =>
            {
                await Db.spDeleteUserAsync(TokenHash, id);
                return NoContent();
            });
        }
    }
}
