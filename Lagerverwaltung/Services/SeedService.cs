using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Identity;

namespace Lagerverwaltung.Services
{
    public class SeedService
    {
        public static async Task SeedDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LagerverwaltungssystemContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedService>>();

            try
            {
                // Ensure the database is ready
                logger.LogInformation("Ensuring database is created...");
                //await context.Database.EnsureCreatedAsync();

                // Seed roles
                logger.LogInformation("Seeding roles...");
                await AddRoleAsync(roleManager, "Administrator");
                await AddRoleAsync(roleManager, "LehrerIn");

                // Seed an initial admin user
                logger.LogInformation("Seeding admin user...");
                await AddUserAsync(userManager, logger,
                    username: "ADMIN",
                    password: "Admin123!",
                    role: "Administrator");

                // Seed a couple of LehrerIn users
                await AddUserAsync(userManager, logger,
                    username: "Dennis",
                    password: "123",
                    role: "LehrerIn");
                await AddUserAsync(userManager, logger,
                    username: "Emily",
                    password: "123",
                    role: "LehrerIn");
                await AddUserAsync(userManager, logger,
                    username: "Michael",
                    password: "123",
                    role: "LehrerIn");

                logger.LogInformation("Seeding completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }
        /// <summary>
        /// Adds a role with the specified name if it does not already exist.
        /// </summary>
        /// <param name="roleManager"></param>
        /// <param name="roleName"></param>
        /// <returns></returns>
        private static async Task AddRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        /// <summary>
        /// Adds a user with the specified username, password, and role if the user does not already exist.
        /// </summary>
        /// <param name="userManager"></param>
        /// <param name="logger"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="role"></param>
        /// <returns></returns>
        private static async Task AddUserAsync(UserManager<Benutzer> userManager, ILogger logger, string username, string password, string role)
        {
            var existingUser = await userManager.FindByNameAsync(username);
            if (existingUser != null)
            {
                return;
            }

            var user = new Benutzer
            {
                UserName = username
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                logger.LogInformation("Assigning role {Role} to user {Username}", role, username);
                await userManager.AddToRoleAsync(user, role);
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogWarning("Failed to create user {Username}: {Errors}", username, errors);
            }
        }
    }
}