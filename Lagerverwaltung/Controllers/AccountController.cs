using Lagerverwaltung.Models;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Lagerverwaltung.Controllers
{
    public class AccountController : Controller
    {
        private readonly LagerverwaltungssystemContext _db;
        private readonly IPasswordHasher<Benutzer> _passwordHasher;

        public AccountController(LagerverwaltungssystemContext db, IPasswordHasher<Benutzer> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login() => View(new LoginViewModel());

        /// <summary>
        /// Login
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            //Checks if ModelState is valid
            if (!ModelState.IsValid)
                return View(model);

            //Checks if User in DB
            var benutzer = await _db.Benutzers
                .Include(b => b.RolleNavigation)
                .FirstOrDefaultAsync(b => b.Username == model.Username);

            if (benutzer == null ||
                _passwordHasher.VerifyHashedPassword(benutzer, benutzer.Password, model.Password)
                    == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Benutzername oder Passwort falsch");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, benutzer.ID.ToString()),
                new(ClaimTypes.Name, benutzer.Username),
                new(ClaimTypes.Role, benutzer.RolleNavigation.Name)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}