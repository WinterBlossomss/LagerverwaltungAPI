using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin + "," + Rollen.Lehrer)]
    public class LaborController : Controller
    {
        private readonly LagerService _lager;

        public LaborController(LagerService lager)
        {
            _lager = lager;
        }

        /// <summary>
        /// Displays the list of labs and their inventory based on the user's role and search criteria.
        /// </summary>
        /// <param name="id">The ID of the lab to display</param>
        /// <param name="suche">The search term for filtering inventory</param>
        /// <returns>The view with the lab information and inventory</returns>
        public async Task<IActionResult> Index(int? id, string suche)
        {
            // checks if user is admin and gets their ID
            var isAdmin = User.IsInRole(Rollen.Admin);
            var uid = User.GetBenutzerId();

            // Admin sees everything, teachers only see their own (the API decides which rooms we get)
            var labore = await _lager.GetLaboreAsync();

            // Create the view model with the list of labs and the search term
            var vm = new LaborViewModel { Auswahl = labore, Suche = suche };
            if (!labore.Any()) return View(vm);

            // If no ID is provided, select the first lab; otherwise, find the lab by ID
            var raum = id == null ? labore.First() : labore.FirstOrDefault(l => l.ID == id);
            if (raum == null) return isAdmin ? NotFound() : Forbid();

            // Get the inventory for the selected lab
            var bestand = await _lager.BestandAsync(raum);

            // Populate the view model with lab details and inventory statistics
            vm.Raum = raum;
            vm.Positionen = bestand.Count;
            vm.Stueck = bestand.Sum(b => b.Menge);
            vm.MitSeriennummer = bestand.Count(b => b.ArtNavigation.SerienNum != null);

            // Filter the inventory based on the search term, if provided, and order by category and article name
            var s = suche?.Trim();
            vm.Bestand = bestand
                .Where(b => string.IsNullOrEmpty(s)
                    || b.ArtNavigation.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || (b.ArtNavigation.SerienNum?.ToString().Contains(s) ?? false))
                .OrderBy(b => b.ArtNavigation.KategorieNavigation?.Name).ThenBy(b => b.ArtNavigation.Name)
                .ToList();

            // Get the latest takeover date for each article in the selected lab and store it in a dictionary
            vm.UebernommenAm = (await _lager.BewegungenAsync(raumId: raum.ID))
                .Where(b => b.ZuRaum == raum.ID && b.BewegungNavigation.Name == BewegungArten.Uebernahme)
                .GroupBy(b => b.Artikel)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Datum));

            // Get the list of open handovers for the selected lab, ordered by date
            vm.OffeneUebergaben = (await _lager.OffeneAusgabenAsync()).Where(b => b.ZuRaum == raum.ID).OrderBy(b => b.Datum).ToList();
            vm.DarfBestaetigen = !isAdmin && raum.Benutzer == uid;
            vm.DarfZurueckgeben = !isAdmin && raum.Benutzer == uid;

            return View(vm);
        }
    }
}
