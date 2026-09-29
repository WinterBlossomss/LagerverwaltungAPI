using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
