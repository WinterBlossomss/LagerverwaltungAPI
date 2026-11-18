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

        //Difference between the two methods is that the first one is a GET method that returns the login view,
        //while the second one is a POST method that handles the login form submission and performs authentication.

        /// <summary>
        /// Login
        /// </summary>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        /// <summary>
        /// Login
        /// </summary>
        /// <param name="model">The login view model.</param>
        /// <param name="returnUrl">The URL to redirect to after login.</param>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            //Checks if ModelState is valid
            if (!ModelState.IsValid)
                return View(model);

            //Checks if User in DB
            var benutzer = await _db.Benutzers
                .Include(b => b.RolleNavigation)
                .FirstOrDefaultAsync(b => b.Username == model.Username);

            //Checks if User is null or Password is incorrect
            var check = benutzer == null
                ? PasswordVerificationResult.Failed
                : _passwordHasher.VerifyHashedPassword(benutzer, benutzer.Password, model.Password);

            if (benutzer == null || check == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Benutzername oder Passwort falsch");
                return View(model);
            }

            // Old Block 1 hash: store it again in the new PBKDF2 format (shared with the REST API).
            if (check == PasswordVerificationResult.SuccessRehashNeeded)
            {
                benutzer.Password = _passwordHasher.HashPassword(benutzer, model.Password);
                await _db.SaveChangesAsync();
            }

            // Create claims for the authenticated user
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, benutzer.ID.ToString()),
                new(ClaimTypes.Name, benutzer.Username),
                new(ClaimTypes.Role, benutzer.RolleNavigation.Name)
            };

            // Create a claims identity and sign in the user
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            // Redirect to the return URL if it's a local URL, otherwise redirect to the dashboard
            if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Dashboard");
        }

        /// <summary>
        /// Logout
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        /// <summary>
        /// Access Denied
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}