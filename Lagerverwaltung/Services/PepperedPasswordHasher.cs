using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Lagerverwaltung.Models;

namespace Lagerverwaltung.Services
{
    public class PepperedPasswordHasher : IPasswordHasher<Benutzer>
    {
        private readonly PasswordHasher<Benutzer> _innerHasher = new();
        private readonly string _pepper;

        /// <summary>
        /// IConfiguration is the built-in ASP.NET Core interface for accessing configuration settings. 
        /// It allows you to retrieve values from various configuration sources, such as appsettings.json, environment variables, and user secrets.
        /// </summary>
        /// <param name="configuration"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public PepperedPasswordHasher(IConfiguration configuration)
        {
            _pepper = configuration["Security:Pepper"]
                ?? throw new InvalidOperationException("Pepper is not configured.");
        }
        /// <summary>
        /// Gets called when user registers or sets a password
        /// </summary>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public string HashPassword(Benutzer user, string password)
        {
            //adds pepper to the password
            var pepperedPassword = password + _pepper;
            return _innerHasher.HashPassword(user, pepperedPassword);
        }
        /// <summary>
        /// Gets called at login, it adds the pepper to the user provided password and checks if it matches the hashed password in the database
        /// </summary>
        /// <param name="user"></param>
        /// <param name="hashedPassword"></param>
        /// <param name="providedPassword"></param>
        /// <returns></returns>
        public PasswordVerificationResult VerifyHashedPassword(
            Benutzer user, string hashedPassword, string providedPassword)
        {
            var pepperedPassword = providedPassword + _pepper;
            return _innerHasher.VerifyHashedPassword(user, hashedPassword, pepperedPassword);
        }
    }
}