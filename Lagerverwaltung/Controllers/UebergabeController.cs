using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize]
    public class UebergabeController : Controller
    {
        private readonly LagerverwaltungssystemContext _db;
        private readonly LagerService _lager;

        public UebergabeController(LagerverwaltungssystemContext db, LagerService lager)
        {
            _db = db;
            _lager = lager;
        }

        /// <summary>
        /// Show the list of pending and confirmed handovers for the logged-in user.
        /// </summary>
        /// <returns></returns>
        [Authorize(Roles = Rollen.Lehrer)]
        public async Task<IActionResult> Index()
        {
            var uid = User.GetBenutzerId();
            return View(new UebergabenViewModel
            {
                Offen = await _lager.OffeneAusgaben()
                    .Where(b => b.ZuRaumNavigation.Benutzer == uid)
                    .OrderBy(b => b.ZuRaumNavigation.RaumNum).ThenBy(b => b.Datum)
                    .ToListAsync(),
                Bestaetigt = await _db.Lagerbewegungens.MitDetails()
                    .Where(b => b.BewegungNavigation.Name == BewegungArten.Uebernahme && b.Benutzer == uid)
                    .OrderByDescending(b => b.Datum).ThenByDescending(b => b.ID)
                    .Take(20)
                    .ToListAsync()
            });
        }

        /// <summary>
        /// Confirm the handover of items to the logged-in user. This action is only accessible to users with the "Lehrer" role.
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
                await _lager.BestaetigenAsync(id, User.GetBenutzerId());
                await _lager.SpeichernAsync();
                TempData["Meldung"] = "Übernahme bestätigt. Die Gegenstände sind jetzt im Laborinventar.";
            }
            catch (LagerException ex)
            {
                TempData["Fehler"] = ex.Message;
            }
            return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Confirm all pending handovers for the specified room. This action is only accessible to users with the "Lehrer" role.
        /// </summary>
        /// <param name="raumId">The ID of the room for which to confirm handovers.</param>
        /// <returns></returns>
        [Authorize(Roles = Rollen.Lehrer)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BestaetigenAlle(int raumId)
        {
            var uid = User.GetBenutzerId();
            var ids = await _lager.OffeneAusgaben()
                .Where(b => b.ZuRaum == raumId && b.ZuRaumNavigation.Benutzer == uid)
                .Select(b => b.ID).ToListAsync();
            try
            {
                foreach (var id in ids) await _lager.BestaetigenAsync(id, uid);
                await _lager.SpeichernAsync();
                TempData["Meldung"] = $"{ids.Count} Position(en) übernommen.";
            }
            catch (LagerException ex)
            {
                TempData["Fehler"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        // Administrator - Ausgabe aus dem Hauptlager

        /// <summary>
        /// Displays the form for creating a new handover from the main storage to a specified room.
        /// This action is only accessible to users with the "Admin" role.
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
        /// Processes the form submission for creating a new handover from the main storage to a specified room.
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
                try
                {
                    foreach (var p in auswahl)
                        await _lager.AusgebenAsync(p.ArtikelId, p.Menge, vm.ZielRaumId!.Value, User.GetBenutzerId());
                    await _lager.SpeichernAsync();

                    var raum = await _lager.GetLabore().FirstAsync(r => r.ID == vm.ZielRaumId);
                    TempData["Meldung"] = $"{auswahl.Count} Position(en) an {raum.BenutzerNavigation.Username} ({raum.Anzeigename}) übergeben. Wartet auf digitale Bestätigung.";
                    return RedirectToAction(nameof(Create));
                }
                catch (LagerException ex)
                {
                    ModelState.AddModelError("", ex.Message);
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

            var bestand = await _db.RaumArtikels
                .Include(ra => ra.ArtNavigation).ThenInclude(a => a.KategorieNavigation)
                .Where(ra => ra.Raum == haupt.ID)
                .ToListAsync();

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

            var labore = await _lager.GetLabore().ToListAsync();
            vm.Labore = labore.Select(l => new SelectListItem($"{l.Anzeigename} · {l.BenutzerNavigation.Username}", l.ID.ToString()));
            vm.Verantwortliche = labore.ToDictionary(l => l.ID, l => l.BenutzerNavigation.Username);
        }
    }
}