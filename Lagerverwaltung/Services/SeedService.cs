using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Services
{
    public static class SeedService
    {
        public static async Task SeedDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LagerverwaltungssystemContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Benutzer>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedIndicator>>();

            try
            {
                logger.LogInformation("Seeding roles...");
                var adminRolle = await GetOrAddRolleAsync(context, "Administrator");
                var lehrerRolle = await GetOrAddRolleAsync(context, "LehrerIn");

                logger.LogInformation("Seeding users...");
                await AddUserAsync(context, hasher, logger, "ADMIN", "Admin123!", adminRolle);
                await AddUserAsync(context, hasher, logger, "Dennis", "123", lehrerRolle);
                await AddUserAsync(context, hasher, logger, "Emily", "123", lehrerRolle);
                await AddUserAsync(context, hasher, logger, "Michael", "123", lehrerRolle);

                logger.LogInformation("Seeding completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }

        private static async Task<Rolle> GetOrAddRolleAsync(LagerverwaltungssystemContext context, string name)
        {
            var rolle = await context.Rolles.FirstOrDefaultAsync(r => r.Name == name);
            if (rolle == null)
            {
                rolle = new Rolle { Name = name };
                context.Rolles.Add(rolle);
                await context.SaveChangesAsync();   // sets Rolle.ID after saving
            }
            return rolle;
        }

        private static async Task AddUserAsync(
            LagerverwaltungssystemContext context,
            IPasswordHasher<Benutzer> hasher,
            ILogger logger,
            string username, string password, Rolle rolle)
        {
            if (await context.Benutzers.AnyAsync(b => b.Username == username))
                return;

            var benutzer = new Benutzer
            {
                Username = username,
                RolleNavigation = rolle
            };
            benutzer.Password = hasher.HashPassword(benutzer, password);

            context.Benutzers.Add(benutzer);
            await context.SaveChangesAsync();
            logger.LogInformation("Created user {Username} with role {Role}", username, rolle.Name);
        }

        // Marker class to indicate that the seeding process has been completed. This is used for logging purposes.
        private sealed class SeedIndicator
        { }
    }
}