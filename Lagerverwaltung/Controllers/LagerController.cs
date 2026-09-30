using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    public class LagerController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
