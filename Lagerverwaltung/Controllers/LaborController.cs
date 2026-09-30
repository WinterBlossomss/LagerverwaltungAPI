using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    public class LaborController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
