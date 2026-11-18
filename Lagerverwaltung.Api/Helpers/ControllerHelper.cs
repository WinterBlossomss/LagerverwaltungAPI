using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Lagerverwaltung.Api.Helpers
{
    public static class ControllerHelper
    {
        /// <summary>
        /// Automatically extracts the token from the Authorization header and returns its SHA256 hash.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static byte[]? GetTokenHash(HttpRequest request)
        {
            var authHeader = request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;

            var rawToken = authHeader.Substring("Bearer ".Length).Trim();
            return SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        }

        /// <summary>
        /// Automatically handles SQL exceptions and returns appropriate HTTP status codes based on the exception number.
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static IActionResult HandleSqlException(ControllerBase controller, SqlException ex)
        {
            if (ex.Number >= 50400 && ex.Number <= 50499)
            {
                int statusCode = ex.Number - 50000;
                return controller.StatusCode(statusCode, new { error = ex.Message });
            }
            return controller.StatusCode(500, new { error = ex.Message });
        }
    }
}