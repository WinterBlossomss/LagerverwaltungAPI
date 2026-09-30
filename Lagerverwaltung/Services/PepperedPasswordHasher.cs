using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Identity;

namespace Lagerverwaltung.Services
{
    public class PepperedPasswordHasher : IPasswordHasher<Benutzer>
    {
        private readonly PasswordHasher<Benutzer> _innerHasher = new();
        private readonly string _pepper;

        /// <summary>
        /// IConfiguration is the built-in ASP.NET Core interface for accessing configuration settings.
        /// It allows you to retrieve values from various configuration sources, such as appsettings.json,
        /// environment variables, and user secrets.
        /// </summary>
        /// <param name="configuration">The configuration object.</param>
        /// <exception cref="InvalidOperationException">Thrown when the pepper is not configured.</exception>
        public PepperedPasswordHasher(IConfiguration configuration)
        {
            _pepper = configuration["Security:Pepper"]
                ?? throw new InvalidOperationException("Pepper is not configured.");
        }

        /// <summary>
        /// Gets called when user registers or sets a password
        /// </summary>
        /// <param name="user">The user for whom to hash the password.</param>
        /// <param name="password">The password to hash.</param>
        /// <returns>The hashed password.</returns>
        public string HashPassword(Benutzer user, string password)
        {
            //adds pepper to the password
            var pepperedPassword = password + _pepper;
            return _innerHasher.HashPassword(user, pepperedPassword);
        }

        /// <summary>
        /// Gets called at login, it adds the pepper to the user provided password and checks if it
        /// matches the hashed password in the database
        /// </summary>
        /// <param name="user">The user for whom to verify the password.</param>
        /// <param name="hashedPassword">The hashed password from the database.</param>
        /// <param name="providedPassword">The password provided by the user.</param>
        /// <returns>The result of the verification.</returns>
        public PasswordVerificationResult VerifyHashedPassword(
            Benutzer user, string hashedPassword, string providedPassword)
        {
            var pepperedPassword = providedPassword + _pepper;
            return _innerHasher.VerifyHashedPassword(user, hashedPassword, pepperedPassword);
        }
    }
}