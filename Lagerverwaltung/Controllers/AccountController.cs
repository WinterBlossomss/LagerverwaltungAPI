using Lagerverwaltung.Services.Api;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Lagerverwaltung.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApiClient _api;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApiClient api, ILogger<AccountController> logger)
        {
            _api = api;
            _logger = logger;
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

            // e.g. "Ihre Sitzung ist abgelaufen" after the API answered 401 (ApiExceptionFilter)
            if (TempData["Fehler"] is string fehler)
                ModelState.AddModelError("", fehler);

            return View(new LoginViewModel());
        }

        /// <summary>
        /// Login: POST /api/auth/login. The API checks the password and returns a bearer token.
        /// The token is stored as a claim in the (encrypted, HttpOnly) authentication cookie.
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

            LoginResponse login;
            try
            {
                login = await _api.LoginAsync(model.Username.Trim(), model.Password);
            }
            catch (ApiException ex) when (ex.IsUnauthorized || ex.StatusCode == 400)
            {
                ModelState.AddModelError("", "Benutzername oder Passwort falsch");
                return View(model);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "REST API not reachable");
                ModelState.AddModelError("", "Die REST-API ist nicht erreichbar. Bitte zuerst Lagerverwaltung.Api starten.");
                return View(model);
            }

            // Create claims for the authenticated user (who, role, and the API token)
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, login.UserId.ToString()),
                new(ClaimTypes.Name, login.Username),
                new(ClaimTypes.Role, login.Role),
                new(ApiClaims.Token, login.Token)
            };

            // The cookie expires together with the token, so nobody stays "logged in" with a dead token.
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTime.SpecifyKind(login.ExpiresAtUtc, DateTimeKind.Utc)
                });

            // Redirect to the return URL if it's a local URL, otherwise redirect to the dashboard
            if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Dashboard");
        }

        /// <summary>
        /// Logout: revokes the token in the API (POST /api/auth/logout), then deletes the cookie.
        /// </summary>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _api.LogoutAsync();
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException)
            {
                // Token already expired or API down: the cookie is deleted anyway.
                _logger.LogInformation(ex, "Logout at the API failed");
            }

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
