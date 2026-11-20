using Lagerverwaltung.Services;
using Lagerverwaltung.Services.Api;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    /// <summary>
    /// Umbuchungslager. With the REST API, "Entgegennehmen" (POST /api/returns/{id}/accept) closes the return
    /// AND books it into the Hauptlager in one step (Einbuchung), so there is no separate "Einbuchen" any more.
    /// </summary>
    [Authorize(Roles = Rollen.Admin)]
    public class UmbuchungController : Controller
    {
        private readonly ApiClient _api;
        private readonly LagerService _lager;

        public UmbuchungController(ApiClient api, LagerService lager)
        {
            _api = api;
            _lager = lager;
        }

        public async Task<IActionResult> Index()
        {
            var umb = await _lager.GetLagerAsync(RaumTypen.Umbuchungslager);
            var offen = (await _lager.OffeneRueckgabenAsync()).OrderBy(b => b.Datum).ToList();
            var bestand = umb == null ? new() : await _lager.BestandAsync(umb);

            return View(new UmbuchungViewModel
            {
                OffeneRueckgaben = offen,
                Bestand = bestand
                    .Select(ra => new UmbuchungBestand
                    {
                        ArtikelId = ra.Art,
                        Name = ra.ArtNavigation.Name,
                        SerienNum = ra.ArtNavigation.SerienNum,
                        Menge = ra.Menge,
                        Einbuchbar = Math.Max(0, ra.Menge - offen.Where(o => o.Artikel == ra.Art).Sum(o => o.ArtikelMenge ?? 1))
                    })
                    .OrderByDescending(b => b.Einbuchbar).ThenBy(b => b.Name)
                    .ToList(),
                Eingebucht = (await _lager.BewegungenAsync())
                    .Where(b => b.BewegungNavigation.Name == BewegungArten.Einbuchung)
                    .Take(10)
                    .ToList()
            });
        }

        /// <summary>POST /api/returns/{id}/accept: Umbuchungslager -> Hauptlager, return closed.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Entgegennehmen(int id)
        {
            try
            {
                await _api.AcceptReturnAsync(id);
                TempData["Meldung"] = "Rückgabe entgegengenommen und ins Hauptlager gebucht.";
            }
            catch (ApiException ex) when (ex.IsUserError)
            {
                TempData["Fehler"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
