using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    public class RueckgabeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
