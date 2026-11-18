using System.Security.Cryptography;
using System.Text;

namespace Lagerverwaltung.Api.Services
{
    /// <summary>
    /// Hashes and verifies passwords with PBKDF2-HMAC-SHA256, a per-password salt and an app-wide pepper.
    ///
    /// How the three parts are combined:
    ///   hash = PBKDF2-HMAC-SHA256( password + pepper, salt, 600 000 iterations, 32 bytes )
    ///
    /// - Salt:   16 random bytes, new for EVERY hash (also on every password change). Stored in the DB.
    ///           Two users with the same password still get different hashes.
    /// - Pepper: secret string from User Secrets / environment variable "Security:Pepper".
    ///           NEVER in the database or in Git. If someone steals the database, the hashes
    ///           are useless without the pepper.
    /// - 600 000 iterations make every guess slow (OWASP recommendation for PBKDF2-SHA256).
    ///
    /// Stored format in Benutzer.Password (fits into NVARCHAR(455)):
    ///   PBKDF2-SHA256$600000$&lt;salt as Base64&gt;$&lt;hash as Base64&gt;
    /// The iteration count is stored too, so it can be raised later without breaking old hashes.
    ///
    /// The MVC app (Lagerverwaltung/Services/PepperedPasswordHasher.cs) uses the same format,
    /// so users can log in to both applications.
    /// </summary>
    public class PasswordService
    {
        private const string Prefix = "PBKDF2-SHA256";
        private const int Iterations = 600_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        private readonly string _pepper;

        public PasswordService(IConfiguration configuration)
        {
            _pepper = configuration["Security:Pepper"]
                ?? throw new InvalidOperationException(
                    "Security:Pepper is not configured. Set it with: dotnet user-secrets set \"Security:Pepper\" \"<secret>\"");
        }

        /// <summary>Creates a new hash with a NEW random salt.</summary>
        public string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Derive(password, salt, Iterations);
            return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Recomputes the hash with the stored salt and iterations and compares in constant time.
        /// Returns false for unknown formats (e.g. old hashes that were never upgraded).
        /// </summary>
        public bool Verify(string password, string storedHash)
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out int iterations))
                return false;

            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);
            byte[] actual = Derive(password, salt, iterations);

            // FixedTimeEquals always compares ALL bytes, so the response time
            // doesn't reveal how many leading bytes were correct.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        /// <summary>
        /// True if the hash is still in the OLD Block 1 format and the password matches it.
        /// The caller must then store a new hash (Hash) right away, so the old format disappears.
        ///
        /// Block 1 used ASP.NET's PasswordHasher (format "V3"), always with password + pepper.
        /// Its bytes are:  0x01 | PRF (4 bytes) | iterations (4) | salt length (4) | salt | hash
        /// PRF 1 = HMAC-SHA256, 2 = HMAC-SHA512. We read these values and recompute PBKDF2 ourselves,
        /// so the API doesn't need ASP.NET Identity.
        /// </summary>
        public bool VerifyLegacy(string password, string storedHash)
        {
            byte[] data;
            try { data = Convert.FromBase64String(storedHash); }
            catch (FormatException) { return false; }

            if (data.Length < 13 || data[0] != 0x01)
                return false;

            uint prf = ReadUInt32BigEndian(data, 1);
            int iterations = (int)ReadUInt32BigEndian(data, 5);
            int saltLength = (int)ReadUInt32BigEndian(data, 9);
            if (saltLength < 1 || 13 + saltLength >= data.Length)
                return false;

            HashAlgorithmName algorithm = prf switch
            {
                1 => HashAlgorithmName.SHA256,
                2 => HashAlgorithmName.SHA512,
                _ => default
            };
            if (algorithm == default)
                return false;

            byte[] salt = data.AsSpan(13, saltLength).ToArray();
            byte[] expected = data.AsSpan(13 + saltLength).ToArray();
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password + _pepper), salt, iterations, algorithm, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset) =>
            (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);

        private byte[] Derive(string password, byte[] salt, int iterations) =>
            Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password + _pepper),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashSize);
    }
}
