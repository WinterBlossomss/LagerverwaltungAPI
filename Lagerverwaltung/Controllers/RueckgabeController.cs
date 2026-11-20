using Lagerverwaltung.Services;
using Lagerverwaltung.Services.Api;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Lehrer)]
    public class RueckgabeController : Controller
    {
        private readonly ApiClient _api;
        private readonly LagerService _lager;

        public RueckgabeController(ApiClient api, LagerService lager)
        {
            _api = api;
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
        /// Books every selected item Labor -> Umbuchungslager (POST /api/returns, one request per item).
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

            if (ModelState.IsValid)
            {
                int gebucht = 0;
                try
                {
                    foreach (var p in auswahl)
                    {
                        await _api.CreateReturnAsync(vm.RaumId, p.ArtikelId, p.Menge);
                        gebucht++;
                    }
                    TempData["Meldung"] = $"{auswahl.Count} Position(en) ins Umbuchungslager gebucht. Der Administrator übernimmt sie ins Hauptlager.";
                    return RedirectToAction(nameof(Index), new { raumId = vm.RaumId });
                }
                catch (ApiException ex) when (ex.IsUserError)
                {
                    // e.g. 409 "Nicht genügend Bestand vorhanden."
                    ModelState.AddModelError("", gebucht == 0
                        ? ex.Message
                        : $"{gebucht} von {auswahl.Count} Position(en) wurden gebucht. Danach: {ex.Message}");
                }
            }

            // If the model state is not valid or an error occurred, refill the view model and return the view
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
            // The API only returns the labs of the logged-in LehrerIn
            vm.Labore = await _lager.GetLaboreAsync();
            if (!vm.Labore.Any()) return true;

            // Determine the selected room based on the provided room ID or default to the first available room
            var raum = vm.RaumId == 0 ? vm.Labore.First() : vm.Labore.FirstOrDefault(l => l.ID == vm.RaumId);
            if (raum == null) return false;

            vm.RaumId = raum.ID;
            vm.RaumName = raum.Anzeigename;

            // Open returns from this lab, newest first
            vm.OffeneRueckgaben = (await _lager.OffeneRueckgabenAsync())
                .Where(b => b.VonRaum == raum.ID)
                .OrderByDescending(b => b.Datum).ThenByDescending(b => b.ID)
                .ToList();

            var gewaehlt = vm.Positionen.Where(p => p.Ausgewaehlt).ToDictionary(p => p.ArtikelId, p => p.Menge);

            // Items in the selected lab
            vm.Positionen = (await _lager.BestandAsync(raum))
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
