using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Lehrer)]
    public class RueckgabeController : Controller
    {
        private readonly LagerverwaltungssystemContext _db;
        private readonly LagerService _lager;

        public RueckgabeController(LagerverwaltungssystemContext db, LagerService lager)
        {
            _db = db;
            _lager = lager;
        }

        /// <summary>
        /// Zeigt die Rückgabe-Seite an, auf der der Benutzer Gegenstände aus einem Labor zurückgeben kann.
        /// </summary>
        /// <param name="raumId">The ID of the room for which to display return options.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Index(int? raumId)
        {
            var vm = new RueckgabeViewModel { RaumId = raumId ?? 0 };
            if (!await BefuelleAsync(vm)) return Forbid();
            return View(vm);
        }

        /// <summary>
        /// Handles the POST request for returning items from a room. Validates the selected items and processes the return.
        /// </summary>
        /// <param name="vm">The view model containing the return information.</param>
        /// <returns>The action result.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(RueckgabeViewModel vm)
        {
            // Validate the selected items for return
            var auswahl = vm.Positionen.Where(p => p.Ausgewaehlt).ToList();
            if (!auswahl.Any()) ModelState.AddModelError("", "Bitte mindestens einen Gegenstand auswählen.");

            // If the model state is valid, process the return of the selected items
            if (ModelState.IsValid)
            {
                try
                {
                    // Process the return of each selected item
                    foreach (var p in auswahl)
                        await _lager.ZurueckgebenAsync(vm.RaumId, p.ArtikelId, p.Menge, User.GetBenutzerId());
                    await _lager.SpeichernAsync();
                    // Set a temporary message indicating the number of items returned and redirect to the index page
                    TempData["Meldung"] = $"{auswahl.Count} Position(en) ins Umbuchungslager gebucht. Der Administrator übernimmt sie ins Hauptlager.";
                    return RedirectToAction(nameof(Index), new { raumId = vm.RaumId });
                }
                catch (LagerException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }

            // If the model state is not valid or an exception occurred, refill the view model and return the view
            if (!await BefuelleAsync(vm)) return Forbid();
            return View(vm);
        }

        /// <summary>
        /// Fills the view model with the necessary data for displaying the return page, including available rooms and items in the selected room.
        /// </summary>
        /// <param name="vm">The view model to fill.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task<bool> BefuelleAsync(RueckgabeViewModel vm)
        {
            // Get the user ID of the currently logged-in user
            var uid = User.GetBenutzerId();
            vm.Labore = await _lager.GetLabore().Where(r => r.Benutzer == uid).ToListAsync();
            if (!vm.Labore.Any()) return true;

            // Determine the selected room based on the provided room ID or default to the first available room
            var raum = vm.RaumId == 0 ? vm.Labore.First() : vm.Labore.FirstOrDefault(l => l.ID == vm.RaumId);
            if (raum == null) return false;

            // Fill the view model with the selected room's ID and name, and retrieve the available items in that room
            vm.RaumId = raum.ID;
            vm.RaumName = raum.Anzeigename;
            var gewaehlt = vm.Positionen.Where(p => p.Ausgewaehlt).ToDictionary(p => p.ArtikelId, p => p.Menge);

            // Retrieve the items in the selected room, including their details and availability, and populate the view model's positions
            vm.Positionen = (await _db.RaumArtikels
                    .Include(ra => ra.ArtNavigation).ThenInclude(a => a.KategorieNavigation)
                    .Where(ra => ra.Raum == raum.ID)
                    .ToListAsync())
                .OrderBy(ra => ra.ArtNavigation.Name)
                .Select(ra => new AuswahlPosition
                {
                    ArtikelId = ra.Art,
                    Name = ra.ArtNavigation.Name,
                    Kategorie = ra.ArtNavigation.KategorieNavigation?.Name,
                    SerienNum = ra.ArtNavigation.SerienNum,
                    Verfuegbar = ra.Menge,
                    Ausgewaehlt = gewaehlt.ContainsKey(ra.Art),
                    Menge = gewaehlt.TryGetValue(ra.Art, out var m) ? m : 1
                }).ToList();
            return true;
        }
    }
}