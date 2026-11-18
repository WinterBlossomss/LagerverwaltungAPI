using Lagerverwaltung.Api.Helpers;
using Lagerverwaltung.Api.Models;
using Lagerverwaltung.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Same database as the MVC app (Block 1). The password is not in the repo:
            // LocalDB uses Windows authentication (Trusted_Connection).
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // EF Core is ONLY used to call stored procedures (context.Procedures), never to query tables.
            builder.Services.AddDbContext<LagerverwaltungssystemContext>(options => options.UseSqlServer(connectionString));

            // Password hashing with salt + pepper. The pepper comes from User Secrets
            // (shared with the MVC app via the same UserSecretsId) or an environment variable.
            builder.Services.AddSingleton<PasswordService>();

            // SqlExceptionFilter turns "THROW 504xx" from the procedures into HTTP status codes.
            builder.Services.AddControllers(options => options.Filters.Add<SqlExceptionFilter>());
            builder.Services.AddOpenApi();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            // Tokens must only travel over HTTPS (task section 4).
            app.UseHttpsRedirection();

            app.MapControllers();

            app.Run();
        }
    }
}
