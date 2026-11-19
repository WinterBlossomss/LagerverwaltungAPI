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
            return await Run(async () =>
            {
                string hash = _passwords.Hash(request.Password);   // new random salt + pepper
                var result = await Db.spRegisterAsync(request.Username.Trim(), hash);
                int id = result.Single().NewId!.Value;

                return Created("/api/users/me", new { id, username = request.Username.Trim(), role = "LehrerIn" });
            });
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
            return await Run(async () =>
            {
                var user = (await Db.spGetUserForLoginAsync(request.Username.Trim())).SingleOrDefault();

                bool ok = user is not null && _passwords.Verify(request.Password, user.PasswordHash);
                bool oldFormat = !ok && user is not null && _passwords.VerifyLegacy(request.Password, user.PasswordHash);

                if (user is null || (!ok && !oldFormat))
                    return Unauthorized(new { error = "Benutzername oder Kennwort falsch." });

                string token = TokenService.CreateToken();
                byte[] tokenHash = TokenService.HashToken(token);
                DateTime expiresAt = DateTime.UtcNow.Add(TokenService.Lifetime);
                await Db.spCreateApiTokenAsync(user.UserId, tokenHash, expiresAt);

                // Users from Block 1 still have the old hash format: store a new PBKDF2 hash (new salt) now.
                if (oldFormat)
                    await Db.spUpdateOwnProfileAsync(tokenHash, null, _passwords.Hash(request.Password));

                return Ok(new LoginResponse(token, expiresAt, user.UserId, user.Username, user.RoleName));
            });
        }

        /// <summary>POST /api/auth/logout: revokes the token that was sent. It stops working immediately.</summary>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            return await Run(async () =>
            {
                await Db.spLogoutAsync(TokenHash);
                return NoContent();
            });
        }
    }
}
