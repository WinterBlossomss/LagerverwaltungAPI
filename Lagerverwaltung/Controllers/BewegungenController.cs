using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class BewegungenController : Controller
    {
        private readonly LagerverwaltungssystemContext _db;

        public BewegungenController(LagerverwaltungssystemContext db) => _db = db;

        /// <summary>
        /// Displays a list of movements based on the provided filter criteria in the BewegungenViewModel.
        /// </summary>
        /// <param name="vm">The view model containing the filter criteria.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<IActionResult> Index(BewegungenViewModel vm)
        {
            // Start with a queryable collection of Lagerbewegungens, including related details.
            var q = _db.Lagerbewegungens.MitDetails().AsQueryable();

            // Apply filters based on the view model properties.
            if (!string.IsNullOrEmpty(vm.Art))
                q = q.Where(b => b.BewegungNavigation.Name == vm.Art);
            if (vm.Status == "offen")
                q = q.Where(b => b.IsDone != true);
            else if (vm.Status == "abgeschlossen")
                q = q.Where(b => b.IsDone == true);
            if (vm.RaumId != null)
                q = q.Where(b => b.VonRaum == vm.RaumId || b.ZuRaum == vm.RaumId);
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                q = q.Where(b => b.ArtikelNavigation.Name.Contains(s)
                    || b.BenutzerNavigation.Username.Contains(s)
                    || (b.ArtikelNavigation.SerienNum != null && b.ArtikelNavigation.SerienNum.ToString()!.Contains(s)));
            }

            // Order the results by date and ID, and take the top 200 entries.
            vm.Bewegungen = await q.OrderByDescending(b => b.Datum).ThenByDescending(b => b.ID).Take(200).ToListAsync();
            // Populate the Arten and Raeume properties for the view model.
            vm.Arten = await _db.BewegungArts.OrderBy(a => a.Name).Select(a => new SelectListItem(a.Name, a.Name)).ToListAsync();
            // Populate the Raeume property with a list of rooms, ordered by type and number.
            vm.Raeume = (await _db.Raums.MitDetails().OrderBy(r => r.RaumTyp).ThenBy(r => r.RaumNum).ToListAsync())
                .Select(r => new SelectListItem(r.Anzeigename, r.ID.ToString()));
            // Return the view with the populated view model.
            return View(vm);
        }
    }
}