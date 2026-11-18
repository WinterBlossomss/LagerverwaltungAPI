using System.Security.Cryptography;
using System.Text;
using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Identity;

namespace Lagerverwaltung.Services
{
    /// <summary>
    /// Password hashing for the MVC app, with the SAME format as the REST API
    /// (Lagerverwaltung.Api/Services/PasswordService.cs), so a user can log in to both:
    ///
    ///   PBKDF2-SHA256$600000$&lt;salt Base64&gt;$&lt;hash Base64&gt;
    ///   hash = PBKDF2-HMAC-SHA256(password + pepper, salt, 600 000 iterations, 32 bytes)
    ///
    /// Older hashes from Block 1 (created with ASP.NET's PasswordHasher) are still accepted once;
    /// the result is then "SuccessRehashNeeded" and the login upgrades them to the new format.
    /// </summary>
    public class PepperedPasswordHasher : IPasswordHasher<Benutzer>
    {
        private const string Prefix = "PBKDF2-SHA256";
        private const int Iterations = 600_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        private readonly PasswordHasher<Benutzer> _legacyHasher = new();
        private readonly string _pepper;

        /// <summary>
        /// IConfiguration is the built-in ASP.NET Core interface for accessing configuration settings.
        /// The pepper is read from User Secrets ("Security:Pepper"), never from the database or Git.
        /// </summary>
        public PepperedPasswordHasher(IConfiguration configuration)
        {
            _pepper = configuration["Security:Pepper"]
                ?? throw new InvalidOperationException("Pepper is not configured.");
        }

        /// <summary>Gets called when a user registers or sets a password. Always uses a NEW random salt.</summary>
        public string HashPassword(Benutzer user, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Derive(password, salt, Iterations);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        /// <summary>Gets called at login: recomputes the hash with the stored salt and compares in constant time.</summary>
        public PasswordVerificationResult VerifyHashedPassword(Benutzer user, string hashedPassword, string providedPassword)
        {
            var parts = hashedPassword.Split('$');
            if (parts.Length == 4 && parts[0] == Prefix && int.TryParse(parts[1], out int iterations))
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] expected = Convert.FromBase64String(parts[3]);
                byte[] actual = Derive(providedPassword, salt, iterations);
                return CryptographicOperations.FixedTimeEquals(actual, expected)
                    ? PasswordVerificationResult.Success
                    : PasswordVerificationResult.Failed;
            }

            // Old Block 1 hash: check it the old way, then ask the caller to re-hash it.
            var legacy = _legacyHasher.VerifyHashedPassword(user, hashedPassword, providedPassword + _pepper);
            return legacy == PasswordVerificationResult.Failed
                ? PasswordVerificationResult.Failed
                : PasswordVerificationResult.SuccessRehashNeeded;
        }

        private byte[] Derive(string password, byte[] salt, int iterations) =>
            Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password + _pepper), salt, iterations, HashAlgorithmName.SHA256, HashSize);
    }
}
