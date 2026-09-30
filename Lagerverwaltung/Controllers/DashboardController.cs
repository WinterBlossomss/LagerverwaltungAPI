using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
