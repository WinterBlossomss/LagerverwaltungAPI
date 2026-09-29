using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<Benutzer> signInManager;
        private readonly UserManager<Benutzer> userManager;
        private readonly RoleManager<IdentityRole> roleManager;

        public AccountController(SignInManager<Benutzer> signInManager, UserManager<Benutzer> userManager, RoleManager<IdentityRole> roleManager)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
    }
}
