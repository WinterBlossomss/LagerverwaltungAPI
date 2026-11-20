using Lagerverwaltung.Services;
using Lagerverwaltung.Services.Api;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lagerverwaltung.Controllers
{
    [Authorize]
    public class UebergabeController : Controller
    {
        private readonly ApiClient _api;
        private readonly LagerService _lager;

        public UebergabeController(ApiClient api, LagerService lager)
        {
            _api = api;
            _lager = lager;
        }

        /// <summary>
        /// Show the list of pending and confirmed handovers for the logged-in user.
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = Rollen.Lehrer)]
        public async Task<IActionResult> Index()
        {
            var name = User.Identity?.Name;
            return View(new UebergabenViewModel
            {
                // The API only returns open handovers for her own labs
                Offen = (await _lager.OffeneAusgabenAsync())
                    .OrderBy(b => b.ZuRaumNavigation.RaumNum).ThenBy(b => b.Datum)
                    .ToList(),
                Bestaetigt = (await _lager.BewegungenAsync())
                    .Where(b => b.BewegungNavigation.Name == BewegungArten.Uebernahme && b.BenutzerNavigation.Username == name)
                    .Take(20)
                    .ToList()
            });
        }

        /// <summary>
        /// Confirm a handover (POST /api/transfers/{id}/confirm). Only the responsible LehrerIn may do this;
        /// a second confirm gives 409.
        /// </summary>
        /// <param name="id">The ID of the handover to confirm.</param>
        /// <param name="returnUrl">The URL to redirect to after confirmation.</param>
        /// <returns></returns>
        [Authorize(Roles = Rollen.Lehrer)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Bestaetigen(int id, string returnUrl)
        {
            try
            {
                await _api.ConfirmTransferAsync(id);
                TempData["Meldung"] = "Übernahme bestätigt. Die Gegenstände sind jetzt im Laborinventar.";
            }
            catch (ApiException ex) when (ex.IsUserError || ex.IsForbidden)
            {
                TempData["Fehler"] = ex.Message;
            }
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Confirm all pending handovers for the specified room.
        /// </summary>
        /// <param name="raumId">The ID of the room for which to confirm handovers.</param>
        /// <returns></returns>
        [Authorize(Roles = Rollen.Lehrer)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BestaetigenAlle(int raumId)
        {
            var ids = (await _lager.OffeneAusgabenAsync())
                .Where(b => b.ZuRaum == raumId)
                .Select(b => b.ID).ToList();

            int bestaetigt = 0;
            try
            {
                foreach (var id in ids)
                {
                    await _api.ConfirmTransferAsync(id);
                    bestaetigt++;
                }
                TempData["Meldung"] = $"{ids.Count} Position(en) übernommen.";
            }
            catch (ApiException ex) when (ex.IsUserError || ex.IsForbidden)
            {
                TempData["Fehler"] = $"{bestaetigt} von {ids.Count} Position(en) übernommen. {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // Administrator - Ausgabe aus dem Hauptlager

        /// <summary>
        /// Displays the form for creating a new handover from the main storage to a specified room.
        /// </summary>
        /// <param name="raumId">The ID of the room to which items will be handed over.</param>
        /// <param name="suche">The search term for filtering items.</param>
        /// <returns></returns>
        [Authorize(Roles = Rollen.Admin)]
        public async Task<IActionResult> Create(int? raumId, string? suche)
        {
            var vm = new AusgabeViewModel { ZielRaumId = raumId, Suche = suche };
            await BefuelleAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Hands the selected items over to a lab (POST /api/transfers, one request per item).
        /// The stock leaves the Hauptlager immediately; the API answers 409 if there is not enough.
        /// </summary>
        /// <param name="vm">The view model for the handover creation form.</param>
        /// <returns></returns>
        [Authorize(Roles = Rollen.Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AusgabeViewModel vm)
        {
            var auswahl = vm.Positionen.Where(p => p.Ausgewaehlt).ToList();
            if (!auswahl.Any()) ModelState.AddModelError("", "Bitte mindestens einen Gegenstand auswählen.");

            if (ModelState.IsValid)
            {
                int gebucht = 0;
                try
                {
                    foreach (var p in auswahl)
                    {
                        await _api.CreateTransferAsync(vm.ZielRaumId!.Value, p.ArtikelId, p.Menge);
                        gebucht++;
                    }

                    var raum = (await _lager.GetLaboreAsync()).First(r => r.ID == vm.ZielRaumId);
                    TempData["Meldung"] = $"{auswahl.Count} Position(en) an {raum.BenutzerNavigation.Username} ({raum.Anzeigename}) übergeben. Wartet auf digitale Bestätigung.";
                    return RedirectToAction(nameof(Create));
                }
                catch (ApiException ex) when (ex.IsUserError)
                {
                    ModelState.AddModelError("", gebucht == 0
                        ? ex.Message
                        : $"{gebucht} von {auswahl.Count} Position(en) wurden übergeben. Danach: {ex.Message}");
                }
            }

            await BefuelleAsync(vm);
            return View(vm);
        }

        private async Task BefuelleAsync(AusgabeViewModel vm)
        {
            var haupt = await _lager.GetLagerAsync(RaumTypen.Hauptlager);
            var s = vm.Suche?.Trim();
            var gewaehlt = vm.Positionen.Where(p => p.Ausgewaehlt).ToDictionary(p => p.ArtikelId, p => p.Menge);

            var bestand = haupt == null ? new() : await _lager.BestandAsync(haupt);

            vm.Positionen = bestand
                .Where(ra => gewaehlt.ContainsKey(ra.Art)
                    || string.IsNullOrEmpty(s)
                    || ra.ArtNavigation.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || (ra.ArtNavigation.SerienNum?.ToString().Contains(s) ?? false))
                .OrderBy(ra => ra.ArtNavigation.KategorieNavigation?.Name).ThenBy(ra => ra.ArtNavigation.Name).ThenBy(ra => ra.ArtNavigation.SerienNum)
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

            var labore = await _lager.GetLaboreAsync();
            vm.Labore = labore.Select(l => new SelectListItem($"{l.Anzeigename} · {l.BenutzerNavigation.Username}", l.ID.ToString()));
            vm.Verantwortliche = labore.ToDictionary(l => l.ID, l => l.BenutzerNavigation.Username);
        }
    }
}
