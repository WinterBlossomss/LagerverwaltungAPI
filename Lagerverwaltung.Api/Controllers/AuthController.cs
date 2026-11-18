using System.Security.Cryptography;
using System.Text;
using Lagerverwaltung.Api.Helpers;
using Lagerverwaltung.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Lagerverwaltung.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly LagerverwaltungssystemContext _context;

        public AuthController(LagerverwaltungssystemContext context)
        {
            _context = context;
        }

        public record LoginRequest(string Username, string Password);

        // POST /api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            //Generate a secure random Bearer token string for the client
            byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
            string plainBearerToken = Convert.ToBase64String(randomBytes);

            //Hash it with SHA-256 to store in the database
            byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(plainBearerToken));

            try
            {
                //Call generated stored procedure
                var result = await _context.Procedures.spLoginAsync(
                    username: request.Username,
                    password: request.Password,
                    tokenHash: tokenHash,
                    tokenLifetimeMinutes: 60
                );

                var userInfo = result.FirstOrDefault();

                //Return the plain Bearer token and user info to the user
                return Ok(new
                {
                    token = plainBearerToken,
                    user = userInfo
                });
            }
            catch (SqlException ex)
            {
                return ControllerHelper.HandleSqlException(this, ex);
            }
        }
    }
}