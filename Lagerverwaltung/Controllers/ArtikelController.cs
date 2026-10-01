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
    public class ArtikelController : Controller
    {
        private readonly LagerverwaltungssystemContext _context;
        private readonly LagerService _lager;

        public ArtikelController(LagerverwaltungssystemContext context, LagerService lager)
        {
            _context = context;
            _lager = lager;
        }

        /// <summary>
        /// Show a list of all articles, optionally filtered by search term and category.
        /// </summary>
        /// <param name="vm">The view model for the article list.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Index(ArtikelListeViewModel vm)
        {
            // Start with all articles, including their categories and manufacturers
            var q = _context.Artikels.Include(a => a.KategorieNavigation).ThenInclude(k => k.HerstellerNavigation).AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                q = q.Where(a => a.Name.Contains(s) || (a.SerienNum != null && a.SerienNum.ToString()!.Contains(s)));
            }
            if (vm.KategorieId != null) q = q.Where(a => a.Kategorie == vm.KategorieId);
            if (vm.HerstellerId != null) q = q.Where(a => a.KategorieNavigation.Hersteller == vm.HerstellerId);

            // Order the articles by name and then by serial number, and load them into the view model
            vm.Artikel = await q.OrderBy(a => a.Name).ThenBy(a => a.SerienNum).ToListAsync();
            vm.Gesamtbestand = await _context.RaumArtikels.GroupBy(r => r.Art)
                .Select(g => new { g.Key, Summe = g.Sum(x => x.Menge) })
                .ToDictionaryAsync(x => x.Key, x => x.Summe);
            vm.Kategorien = await KategorienAsync();
            vm.Hersteller = await HerstellerAsync();
            return View(vm);
        }

        /// <summary>
        /// Show the details of a specific article, including its locations and movements.
        /// </summary>
        /// <param name="id">The ID of the article to display.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Details(int? id)
        {
            // Validate the ID and retrieve the article from the database, returning NotFound if not found
            if (id == null) return NotFound();
            var artikel = await _context.Artikels.Include(a => a.KategorieNavigation).ThenInclude(k => k.HerstellerNavigation)
                .FirstOrDefaultAsync(a => a.ID == id);
            if (artikel == null) return NotFound();

            // Retrieve the locations and movements of the article, including related data, and order the movements by date and ID
            var standorte = await _context.RaumArtikels
                .Include(r => r.RaumNavigation).ThenInclude(r => r.RaumTypNavigation)
                .Include(r => r.RaumNavigation).ThenInclude(r => r.BenutzerNavigation)
                .Where(r => r.Art == id).ToListAsync();

            // Retrieve the movements of the article, including related data, and order them by date and ID
            var bewegungen = await _context.Lagerbewegungens.MitDetails()
                .Where(b => b.Artikel == id).OrderByDescending(b => b.Datum).ThenByDescending(b => b.ID).ToListAsync();

            // Return the view with the article details, locations, movements, and a flag indicating whether the article can be deleted
            return View(new ArtikelDetailsViewModel
            {
                Artikel = artikel,
                Standorte = standorte,
                Bewegungen = bewegungen,
                KannGeloeschtWerden = !bewegungen.Any() && !standorte.Any()
            });
        }

        /// <summary>
        /// Show the form to create a new article, with a list of categories to choose from.
        /// </summary>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Create()
        {
            return View(new ArtikelFormViewModel { Kategorien = await KategorienAsync() });
        }

        /// <summary>
        /// Handle the submission of the form to create a new article, validating the input and saving it to the database if valid.
        /// </summary>
        /// <param name="vm">The view model containing the article data.</param>
        /// <returns>The action result.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ArtikelFormViewModel vm)
        {
            // Remove the validation for Anfangsbestand, since it is only relevant for new articles
            await ValidiereAsync(vm);
            if (vm.SerienNum != null && vm.Anfangsbestand != 1)
                ModelState.AddModelError(nameof(vm.Anfangsbestand), "Ein Gerät mit Seriennummer hat immer den Bestand 1.");

            // Validate the model state and return the view with the categories if invalid
            if (!ModelState.IsValid)
            {
                vm.Kategorien = await KategorienAsync();
                return View(vm);
            }

            //// Generate a new ID for the article by finding the maximum existing ID and adding 1, or using 1 if there are no articles
            var neueId = (await _context.Artikels.MaxAsync(a => (int?)a.ID) ?? 0) + 1;
            _context.Artikels.Add(new Artikel { ID = neueId, Name = vm.Name.Trim(), SerienNum = vm.SerienNum, Kategorie = vm.KategorieId!.Value });
            await _lager.ZugangAsync(neueId, vm.Anfangsbestand, User.GetBenutzerId());
            await _lager.SpeichernAsync();

            TempData["Meldung"] = $"{vm.Name} wurde erfasst und ins Hauptlager gebucht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Show the form to edit an existing article, pre-populated with its current data and a list of categories to choose from.
        /// </summary>
        /// <param name="id">The ID of the article to edit.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Edit(int? id)
        {
            // Validate the ID and retrieve the article from the database, returning NotFound if not found
            if (id == null) return NotFound();
            var artikel = await _context.Artikels.FindAsync(id);
            if (artikel == null) return NotFound();

            // Return the view with the article data and the list of categories for selection
            return View(new ArtikelFormViewModel
            {
                ID = artikel.ID,
                Name = artikel.Name,
                SerienNum = artikel.SerienNum,
                KategorieId = artikel.Kategorie,
                Kategorien = await KategorienAsync()
            });
        }

        /// <summary>
        /// Update an existing article with the provided data.
        /// </summary>
        /// <param name="id">The ID of the article to update.</param>
        /// <param name="vm">The view model containing the updated article data.</param>
        /// <returns>The action result.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ArtikelFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();
            var artikel = await _context.Artikels.FindAsync(id);
            if (artikel == null) return NotFound();

            ModelState.Remove(nameof(vm.Anfangsbestand));
            await ValidiereAsync(vm);
            if (vm.SerienNum != null && await _context.RaumArtikels.Where(r => r.Art == id).SumAsync(r => r.Menge) > 1)
                ModelState.AddModelError(nameof(vm.SerienNum), "Dieser Artikel hat einen Bestand größer 1 und kann keine Seriennummer bekommen.");

            if (!ModelState.IsValid)
            {
                vm.Kategorien = await KategorienAsync();
                return View(vm);
            }

            artikel.Name = vm.Name.Trim();
            artikel.SerienNum = vm.SerienNum;
            artikel.Kategorie = vm.KategorieId!.Value;
            await _context.SaveChangesAsync();

            TempData["Meldung"] = "Änderungen gespeichert.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Show the form to delete an existing article.
        /// </summary>
        /// <param name="id">The ID of the article to delete.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            return await Details(id);
        }

        /// <summary>
        /// Delete an existing article from the database, if it is not in use in any locations or movements.
        /// </summary>
        /// <param name="id">The ID of the article to delete.</param>
        /// <returns>The action result.</returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var artikel = await _context.Artikels.FindAsync(id);
            if (artikel == null) return NotFound();

            var inVerwendung = await _context.RaumArtikels.AnyAsync(r => r.Art == id)
                || await _context.Lagerbewegungens.AnyAsync(b => b.Artikel == id);
            if (inVerwendung)
            {
                TempData["Fehler"] = "Der Artikel hat Bestand oder Lagerbewegungen und kann nicht gelöscht werden.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Artikels.Remove(artikel);
            await _context.SaveChangesAsync();
            TempData["Meldung"] = $"{artikel.Name} wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Validate the article form view model, checking for duplicate serial numbers and adding model errors if necessary.
        /// </summary>
        /// <param name="vm">The article form view model to validate.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ValidiereAsync(ArtikelFormViewModel vm)
        {
            if (vm.SerienNum != null && await _context.Artikels.AnyAsync(a => a.SerienNum == vm.SerienNum && a.ID != vm.ID))
                ModelState.AddModelError(nameof(vm.SerienNum), "Diese Seriennummer ist bereits vergeben.");
        }

        /// <summary>
        /// Get a list of categories from the database, ordered by name,
        /// and return them as a list of SelectListItem objects for use in a dropdown menu.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task<IEnumerable<SelectListItem>> KategorienAsync()
        {
            var daten = await _context.Kategories
                .Select(k => new { k.ID, k.Name, Hersteller = k.HerstellerNavigation.Name })
                .ToListAsync();
            var gruppen = daten.Select(d => d.Hersteller ?? "Ohne Hersteller").Distinct()
                .ToDictionary(n => n, n => new SelectListGroup { Name = n });
            return daten.OrderBy(d => d.Hersteller).ThenBy(d => d.Name)
                .Select(d => new SelectListItem(d.Name, d.ID.ToString()) { Group = gruppen[d.Hersteller ?? "Ohne Hersteller"] })
                .ToList();
        }

        /// <summary>
        /// Get a list of manufacturers from the database, ordered by name, and return them as a list of SelectListItem objects for use in a dropdown menu.
        /// </summary>
        /// <returns></returns>
        private async Task<IEnumerable<SelectListItem>> HerstellerAsync() =>
            await _context.Herstellers.OrderBy(h => h.Name).Select(h => new SelectListItem(h.Name, h.ID.ToString())).ToListAsync();
    }
}