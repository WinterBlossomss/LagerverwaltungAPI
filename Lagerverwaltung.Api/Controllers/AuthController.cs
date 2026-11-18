using Lagerverwaltung.Api.DTOs;
using Lagerverwaltung.Api.Models;
using Lagerverwaltung.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Api.Controllers
{
    [Route("api/auth")]
    public class AuthController : ApiControllerBase
    {
        private readonly PasswordService _passwords;

        public AuthController(LagerverwaltungssystemContext context, PasswordService passwords) : base(context)
        {
            _passwords = passwords;
        }

        /// <summary>
        /// POST /api/auth/register  (public)
        /// Always creates a 'LehrerIn'. There is no role field: spRegister sets the role itself.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            string hash = _passwords.Hash(request.Password);   // new random salt + pepper
            var result = await Db.spRegisterAsync(request.Username.Trim(), hash);
            int id = result.Single().NewId!.Value;

            return Created("/api/users/me", new { id, username = request.Username.Trim(), role = "LehrerIn" });
        }

        /// <summary>
        /// POST /api/auth/login  (public)
        /// 1. spGetUserForLogin fetches the stored hash (the DB never sees the plain password).
        /// 2. The password is verified here in C# with salt + pepper.
        /// 3. A random token is created; only its SHA-256 hash is stored (spCreateApiToken).
        /// 4. The raw token is returned exactly once.
        /// Wrong username and wrong password give the SAME answer, so usernames can't be probed.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = (await Db.spGetUserForLoginAsync(request.Username.Trim())).SingleOrDefault();

            if (user is null || !_passwords.Verify(request.Password, user.PasswordHash))
                return Unauthorized(new { error = "Benutzername oder Kennwort falsch." });

            string token = TokenService.CreateToken();
            DateTime expiresAt = DateTime.UtcNow.Add(TokenService.Lifetime);
            await Db.spCreateApiTokenAsync(user.UserId, TokenService.HashToken(token), expiresAt);

            return Ok(new LoginResponse(token, expiresAt, user.UserId, user.Username, user.RoleName));
        }

        /// <summary>POST /api/auth/logout: revokes the token that was sent. It stops working immediately.</summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await Db.spLogoutAsync(TokenHash);
            return NoContent();
        }
    }
}
