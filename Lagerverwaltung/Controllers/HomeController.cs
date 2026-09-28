using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace Lagerverwaltung.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private readonly IPasswordHasher<Benutzer> _passwordHasher;

        public HomeController(ILogger<HomeController> logger, IPasswordHasher<Benutzer> PasswordHasher)
        {
            _logger = logger;
            _passwordHasher = PasswordHasher;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        public async Task<IActionResult> Login()
        {
            Benutzer benutzer = new Benutzer();
            string pepper = "MyLittlePony";
            string password = benutzer.Password + pepper;
            string hash = _passwordHasher.HashPassword(benutzer, password);

            if (true)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, benutzer.Username),
                    new Claim(ClaimTypes.Sid, benutzer.ID.ToString()),
                    new Claim(ClaimTypes.Role, benutzer.RolleNavigation.Name)
                };
                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
                await HttpContext.SignInAsync(claimsPrincipal);
            }
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}