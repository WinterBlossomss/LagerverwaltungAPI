using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class BewegungenController : Controller
    {
        private readonly LagerService _lager;

        public BewegungenController(LagerService lager) => _lager = lager;

        /// <summary>
        /// Displays a list of movements based on the provided filter criteria in the BewegungenViewModel.
        /// The room filter is done by the API (GET /api/movements?raumId=), the others here.
        /// </summary>
        /// <param name="vm">The view model containing the filter criteria.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<IActionResult> Index(BewegungenViewModel vm)
        {
            IEnumerable<Models.Lagerbewegungen> q = await _lager.BewegungenAsync(raumId: vm.RaumId);

            // Apply filters based on the view model properties.
            if (!string.IsNullOrEmpty(vm.Art))
                q = q.Where(b => b.BewegungNavigation.Name == vm.Art);
            if (vm.Status == "offen")
                q = q.Where(b => b.IsDone != true);
            else if (vm.Status == "abgeschlossen")
                q = q.Where(b => b.IsDone == true);
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                q = q.Where(b => b.ArtikelNavigation.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || b.BenutzerNavigation.Username.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || (b.ArtikelNavigation.SerienNum?.ToString().Contains(s) ?? false));
            }

            // Newest first (already sorted by LagerService), top 200 entries.
            vm.Bewegungen = q.Take(200).ToList();
            vm.Arten = BewegungArten.Alle.Order().Select(a => new SelectListItem(a, a)).ToList();
            vm.Raeume = (await _lager.GetRaeumeAsync()).Select(r => new SelectListItem(r.Anzeigename, r.ID.ToString()));
            return View(vm);
        }
    }
}
