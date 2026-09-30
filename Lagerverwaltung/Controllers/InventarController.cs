using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    public class InventarController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
