using Lagerverwaltung.Services;
using Lagerverwaltung.Services.Api;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Lagerverwaltung
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Block 2: no DbContext. All data comes from the REST API (Lagerverwaltung.Api).
            var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
                ?? throw new InvalidOperationException("Setting 'Api:BaseUrl' not found (appsettings.json).");

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddTransient<BearerTokenHandler>();
            builder.Services.AddHttpClient<ApiClient>(client =>
                {
                    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
                    client.Timeout = TimeSpan.FromSeconds(30);
                })
                .AddHttpMessageHandler<BearerTokenHandler>();
            builder.Services.AddScoped<LagerService>();

            // The cookie only says "who is logged in" and carries the API token (claim "api_token").
            // It expires together with the token (see AccountController.Login).
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.SlidingExpiration = false;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            builder.Services.AddControllersWithViews(o =>
            {
                o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                o.Filters.Add<ApiExceptionFilter>();   // 401/403/404/409 from the API -> login, access denied, 404, message
            });

            builder.Services.AddAuthorization();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
