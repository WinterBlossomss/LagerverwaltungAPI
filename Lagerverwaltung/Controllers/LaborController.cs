using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin + "," + Rollen.Lehrer)]
    public class LaborController : Controller
    {
        private readonly LagerverwaltungssystemContext _db;
        private readonly LagerService _lager;

        public LaborController(LagerverwaltungssystemContext db, LagerService lager)
        {
            _db = db;
            _lager = lager;
        }

        // GET: /Labor/Index/5?suche=...
        public async Task<IActionResult> Index(int? id, string suche)
        {
            // checks if user is admin and gets their ID
            var isAdmin = User.IsInRole(Rollen.Admin);
            var uid = User.GetBenutzerId();

            // Admin sees everything, teachers only see their own
            var labore = isAdmin
                ? await _lager.GetLabore().ToListAsync()
                : await _lager.GetLabore().Where(r => r.Benutzer == uid).ToListAsync();

            // Create the view model with the list of labs and the search term
            var vm = new LaborViewModel { Auswahl = labore, Suche = suche };
            if (!labore.Any()) return View(vm);

            // If no ID is provided, select the first lab; otherwise, find the lab by ID
            var raum = id == null ? labore.First() : labore.FirstOrDefault(l => l.ID == id);
            if (raum == null) return isAdmin ? NotFound() : Forbid();

            // Get the inventory for the selected lab, including related article and category data
            var bestand = await _db.RaumArtikels
                .Include(ra => ra.ArtNavigation).ThenInclude(a => a.KategorieNavigation)
                .Where(ra => ra.Raum == raum.ID)
                .ToListAsync();

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
            vm.UebernommenAm = (await _db.Lagerbewegungens
                    .Where(b => b.ZuRaum == raum.ID && b.BewegungNavigation.Name == BewegungArten.Uebernahme)
                    .Select(b => new { b.Artikel, b.Datum })
                    .ToListAsync())
                .GroupBy(b => b.Artikel)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Datum));

            // Get the list of open handovers for the selected lab, ordered by date
            vm.OffeneUebergaben = await _lager.OffeneAusgaben().Where(b => b.ZuRaum == raum.ID).OrderBy(b => b.Datum).ToListAsync();
            vm.DarfBestaetigen = !isAdmin && raum.Benutzer == uid;
            vm.DarfZurueckgeben = !isAdmin && raum.Benutzer == uid;

            return View(vm);
        }
    }
}