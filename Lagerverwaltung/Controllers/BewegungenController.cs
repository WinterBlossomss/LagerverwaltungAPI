using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    public class BewegungenController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
