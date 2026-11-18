using System.Security.Cryptography;
using System.Text;

namespace Lagerverwaltung.Api.Services
{
    /// <summary>
    /// Opaque bearer tokens (no JWT).
    ///
    /// - The token is 32 cryptographically random bytes, Base64Url encoded. It contains no user
    ///   data and no role: it is just a random "key". Who it belongs to is only known by the database.
    /// - The client gets the raw token ONCE (login response) and sends it as "Authorization: Bearer &lt;token&gt;".
    /// - The database only stores SHA-256(token). If the ApiTokens table leaks, the hashes can't be used to log in.
    ///   No salt is needed here: 256 random bits can't be guessed, unlike a password.
    /// </summary>
    public static class TokenService
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

        public static string CreateToken()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            // Base64Url: no '+', '/' or '=' characters, so the token is safe in headers and .http files.
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

        /// <summary>
        /// Reads "Authorization: Bearer &lt;token&gt;" and returns SHA-256 of the token,
        /// or null if the header is missing. A null hash makes spAuthorize answer 401.
        /// </summary>
        public static byte[]? GetTokenHash(HttpRequest request)
        {
            string? header = request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;

            string token = header["Bearer ".Length..].Trim();
            return token.Length == 0 ? null : HashToken(token);
        }
    }
}
